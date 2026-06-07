using Microsoft.Extensions.Caching.Distributed;
using NSubstitute.ReturnsExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.AzureStorage;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.Services;
using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.Fixtures;

public sealed class TableEventStoreFixture : IAsyncInitializer, IAsyncDisposable
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
        bool removeFromCacheOnDelete = false,
        int snapshotRecalculationInterval = 1
    )
        where TAggregate : class, IAggregate, new() =>
        CreateEventStoreContext(
            aggregateChangeNotifier,
            removeFromCacheOnDelete,
            snapshotRecalculationInterval
        ).EventStore;

    internal (
        TableEventStore<TAggregate> EventStore,
        ITableEventStoreTelemetry Telemetry,
        IDistributedCache Cache,
        AzureTableClient TableClient,
        AzureBlobClient BlobClient
    ) CreateEventStoreContext<TAggregate>(
        IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
        bool removeFromCacheOnDelete = false,
        int snapshotRecalculationInterval = 1
    )
        where TAggregate : class, IAggregate, new()
    {
        var runId = Guid.NewGuid();

        var tableName = TestHelpers.GenAzureTableName(runId);
        var containerName = TestHelpers.GenAzureBlobContainerName(runId);

        _tableName = tableName;
        _containerName = containerName;

        var cache = CreateDistributedCache();
        Cache = cache;

        var telemetry = Substitute.For<ITableEventStoreTelemetry>();
        Telemetry = telemetry;

        _eventNameMapper = new AggregateEventNameMapper();

        var aggregateRequirementsManager = Substitute.For<IAggregateRequirementsManager>();
        AzureStorageEventStoreOptions azureStorageOptions = new()
        {
            ConnectionString = _azuriteContainer.GetConnectionString(),
            Table = tableName,
            Container = containerName,
            TimeoutInSeconds = 10,
            RemoveDeletedFromCache = removeFromCacheOnDelete,
            SnapshotInterval = snapshotRecalculationInterval,
        };

        TableEventStore<TAggregate> eventStore = new(
            eventNameMapper: _eventNameMapper,
            azureStorageOptions: Microsoft.Extensions.Options.Options.Create(azureStorageOptions),
            distributedCache: cache,
            aggregateChangeNotifier: aggregateChangeNotifier
                ?? Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>(),
            eventStoreTelemetry: telemetry,
            aggregateRequirementsManager: aggregateRequirementsManager
        );

        var tableClient = new AzureTableClient(azureStorageOptions, eventStore.TableName);
        TableClient = tableClient;

        var blobClient = new AzureBlobClient(azureStorageOptions, eventStore.ContainerName);
        BlobClient = blobClient;

        _eventStoreAsDisposable = eventStore as IDisposable;

        return (eventStore, telemetry, cache, tableClient, blobClient);
    }

    public static IDistributedCache CreateDistributedCache()
    {
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNullForAnyArgs();

        return cache;
    }

    public async Task InitializeAsync() => await _azuriteContainer.StartAsync();

    public async ValueTask DisposeAsync()
    {
        _eventStoreAsDisposable?.Dispose();

        await _azuriteContainer.DisposeAsync();
    }
}
