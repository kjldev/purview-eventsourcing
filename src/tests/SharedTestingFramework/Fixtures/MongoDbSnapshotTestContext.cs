using Microsoft.Extensions.Caching.Distributed;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.AzureStorage;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.MongoDB.Snapshots;
using Purview.EventSourcing.MongoDB.StorageClient;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.Fixtures;

public sealed class MongoDBSnapshotTestContext
{
	readonly string _mongoDbConnectionString;
	readonly string _azuriteConnectionString;

	ITableEventStoreTelemetry _telemetry = default!;
	IAggregateEventNameMapper _eventNameMapper = default!;

	public Guid RunId { get; } = Guid.NewGuid();

	internal MongoDBClient MongoDBClient { get; private set; } = default!;

	internal AzureTableClient TableClient { get; private set; } = default!;

	internal AzureBlobClient BlobClient { get; private set; } = default!;

	public MongoDBSnapshotEventStore<PersistenceAggregate> EventStore { get; init; }

	public MongoDBSnapshotTestContext(
		string mongoDbConnectionString,
		string azuriteConnectionString,
		int correlationIdsToGenerate = 1,
		string? collectionName = null
	)
	{
		_mongoDbConnectionString = mongoDbConnectionString;
		_azuriteConnectionString = azuriteConnectionString;

		EventStore = CreateMongoDBEventStore(correlationIdsToGenerate, collectionName);
	}

	public MongoDBSnapshotEventStore<PersistenceAggregate> CreateMongoDBEventStore(
		int correlationIdsToGenerate = 1,
		string? collectionName = null
	)
	{
		var tableEventStore = CreateTableEventStore(correlationIdsToGenerate);

		MongoDBSnapshotEventStoreOptions config = new()
		{
			ConnectionString = _mongoDbConnectionString,
			Database = GetType().Name,
			Collection = collectionName ?? TestHelpers.GenMongoDBCollectionName(),
		};

		MongoDBSnapshotEventStore<PersistenceAggregate> eventStore = new(
			tableEventStore,
			Microsoft.Extensions.Options.Options.Create(config),
			Substitute.For<IMongoDBSnapshotEventStoreTelemetry>(),
			Substitute.For<IMongoDBClientTelemetry>()
		);

		MongoDBClient = new(
			Substitute.For<IMongoDBClientTelemetry>(),
			new()
			{
				ConnectionString = config.ConnectionString,
				ApplicationName = "purview-integration-tests",
				Database = config.Database,
				Collection = config.Collection,
				ReplicaName = "rs0",
			}
		);

		return eventStore;
	}

	TableEventStore<PersistenceAggregate> CreateTableEventStore(int correlationIdsToGenerate = 1)
	{
		var runIds = Enumerable
			.Range(1, correlationIdsToGenerate)
			.Select(_ => $"{Guid.NewGuid()}".ToUpperInvariant())
			.ToArray();

		_eventNameMapper = new AggregateEventNameMapper();
		_telemetry = Substitute.For<ITableEventStoreTelemetry>();

		AzureStorageEventStoreOptions azureStorageOptions = new()
		{
			ConnectionString = _azuriteConnectionString,
			Table = TestHelpers.GenAzureTableName(RunId),
			Container = TestHelpers.GenAzureBlobContainerName(RunId),
			TimeoutInSeconds = 10,
			RemoveDeletedFromCache = true,
			SnapshotInterval = 1,
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
}
