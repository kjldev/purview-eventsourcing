using Microsoft.Extensions.Caching.Distributed;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.AzureStorage;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.CosmosDb;
using Purview.EventSourcing.CosmosDb.Snapshot;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.SnapshotOnly.CosmosDb;

public sealed class CosmosDbSnapshotEventStoreContext(string cosmosDbConnectionString, HttpClient cosmosDbHttpClient, string azuriteConnectionString) : IAsyncDisposable
{
	ITableEventStoreTelemetry _telemetry = default!;
	IAggregateEventNameMapper _eventNameMapper = default!;

	public Guid RunId { get; } = Guid.NewGuid();

	public CosmosDbSnapshotEventStore<PersistenceAggregate> EventStore { get; private set; } = default!;

	internal AzureTableClient TableClient { get; private set; } = default!;

	internal AzureBlobClient BlobClient { get; private set; } = default!;

	internal CosmosDbClient CosmosDbClient { get; private set; } = default!;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Used elsewhere.")]
	public void CreateCosmosDbEventStore(int correlationIdsToGenerate = 1, string? container = null)
	{
		var tableEventStore = CreateTableEventStore(correlationIdsToGenerate);

		CosmosDbEventStoreOptions config = new()
		{
			Container = container ?? TestHelpers.GenAzureCosmosDbContainerName(),
			ConnectionString = cosmosDbConnectionString,
			Database = GetType().Name,
			RequestTimeoutInSeconds = 30,
			IgnoreSSLWarnings = true,
			ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway
		};

		Microsoft.Azure.Cosmos.CosmosClient cosmosClient = new(config.ConnectionString, clientOptions: new Microsoft.Azure.Cosmos.CosmosClientOptions()
		{
			HttpClientFactory = () => cosmosDbHttpClient,
			ConnectionMode = config.ConnectionMode,
			LimitToEndpoint = true,
			Serializer = new CosmosDbClient.CosmosJsonNetSerializer(JsonHelpers.JsonSerializerSettings)
		});

		CosmosDbClient = new(
			config,
			$"/{nameof(IAggregate.AggregateType)}",
			null,
			cosmosClient
		);

		CosmosDbSnapshotEventStore<PersistenceAggregate> eventStore = new(
			tableEventStore,
			Microsoft.Extensions.Options.Options.Create(config),
			Substitute.For<ICosmosDbSnapshotEventStoreTelemetry>(),
			cosmosClient
		);

		EventStore = eventStore;
	}

	TableEventStore<PersistenceAggregate> CreateTableEventStore(int correlationIdsToGenerate = 1)
	{
		var runIds = Enumerable.Range(1, correlationIdsToGenerate).Select(_ => $"{Guid.NewGuid()}".ToUpperInvariant()).ToArray();

		_eventNameMapper = new AggregateEventNameMapper();
		_telemetry = Substitute.For<ITableEventStoreTelemetry>();

		AzureStorageEventStoreOptions azureStorageOptions = new()
		{
			ConnectionString = azuriteConnectionString,
			Table = TestHelpers.GenAzureTableName(RunId),
			Container = TestHelpers.GenAzureBlobContainerName(RunId),
			TimeoutInSeconds = 10,
			RemoveDeletedFromCache = true,
			SnapshotInterval = 1
		};

		TableEventStore<PersistenceAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			azureStorageOptions: Microsoft.Extensions.Options.Options.Create(azureStorageOptions),
			distributedCache: Substitute.For<IDistributedCache>(),
			aggregateChangeNotifier: Substitute.For<IAggregateChangeFeedNotifier<PersistenceAggregate>>(),
			eventStoreTelemetry: _telemetry,
			aggregateRequirementsManager: Substitute.For<IAggregateRequirementsManager>()
		);

		TableClient = new(azureStorageOptions, eventStore.TableName);
		BlobClient = new(azureStorageOptions, eventStore.ContainerName);

		return eventStore;
	}

	public async ValueTask DisposeAsync()
		=> await CosmosDbClient.DeleteContainerAsync();
}
