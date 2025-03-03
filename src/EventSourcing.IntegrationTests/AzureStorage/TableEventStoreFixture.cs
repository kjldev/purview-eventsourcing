using Microsoft.Extensions.Caching.Distributed;
using NSubstitute.ReturnsExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.AzureStorage;

public sealed class TableEventStoreFixture : IAsyncLifetime
{
	readonly Testcontainers.Azurite.AzuriteContainer _azuriteContainer;

	string _containerName = default!;
	string _tableName = default!;

	IAggregateEventNameMapper _eventNameMapper = default!;
	IDisposable? _eventStoreAsDisposable;

	public TableEventStoreFixture()
	{
		_azuriteContainer = ContainerHelper.CreateAzurite();
	}

	public IDistributedCache Cache { get; private set; } = default!;

	public ITableEventStoreTelemetry Telemetry { get; private set; } = default!;

	internal AzureTableClient TableClient { get; private set; } = default!;

	internal AzureBlobClient BlobClient { get; private set; } = default!;

	public TableEventStore<TAggregate> CreateEventStore<TAggregate>(
		IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
		int correlationIdsToGenerate = 1,
		bool removeFromCacheOnDelete = false,
		int snapshotRecalculationInterval = 1)
		where TAggregate : class, IAggregate, new()
	{
		var runId = Guid.NewGuid();
		var runIds = Enumerable.Range(1, correlationIdsToGenerate)
			.Select(_ => $"{Guid.NewGuid()}".ToUpperInvariant())
			.ToArray();

		_tableName = TestHelpers.GenAzureTableName(runId);
		_containerName = TestHelpers.GenAzureBlobContainerName(runId);

		Cache = CreateDistributedCache();
		Telemetry = Substitute.For<ITableEventStoreTelemetry>();
		_eventNameMapper = new AggregateEventNameMapper();

		var aggregateRequirementsManager = Substitute.For<IAggregateRequirementsManager>();
		AzureStorageEventStoreOptions azureStorageOptions = new()
		{
			ConnectionString = _azuriteContainer.GetConnectionString(),
			Table = _tableName,
			Container = _containerName,
			TimeoutInSeconds = 10,
			RemoveDeletedFromCache = removeFromCacheOnDelete,
			SnapshotInterval = snapshotRecalculationInterval
		};

		TableEventStore<TAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			azureStorageOptions: Microsoft.Extensions.Options.Options.Create(azureStorageOptions),
			distributedCache: Cache,
			aggregateChangeNotifier: aggregateChangeNotifier ?? Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>(),
			eventStoreTelemetry: Telemetry,
			aggregateRequirementsManager: aggregateRequirementsManager
		);

		TableClient = new(azureStorageOptions, eventStore.TableName);
		BlobClient = new(azureStorageOptions, eventStore.ContainerName);

		_eventStoreAsDisposable = eventStore as IDisposable;

		return eventStore;
	}

	public static IDistributedCache CreateDistributedCache()
	{
		var cache = Substitute.For<IDistributedCache>();
		cache
			.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.ReturnsNullForAnyArgs();

		return cache;
	}

	public async ValueTask InitializeAsync() => await _azuriteContainer.StartAsync();

	public async ValueTask DisposeAsync()
	{
		_eventStoreAsDisposable?.Dispose();

		await _azuriteContainer.DisposeAsync();
	}
}
