using Microsoft.Extensions.Caching.Distributed;
using NSubstitute.ReturnsExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.MongoDB.StorageClients;
using Purview.EventSourcing.Services;
using Testcontainers.MongoDb;
using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.MongoDB;

public sealed class MongoDBEventStoreFixture : IAsyncInitializer, IAsyncDisposable
{
	readonly MongoDbContainer _mongoDBContainer;

	IAggregateEventNameMapper _eventNameMapper = default!;

	public MongoDBEventStoreFixture()
	{
		_mongoDBContainer = ContainerHelper.CreateMongoDB();
	}

	public IDistributedCache Cache { get; private set; } = default!;

	public IMongoDBEventStoreTelemetry Telemetry { get; private set; } = default!;

	internal MongoDBClient EventClient { get; private set; } = default!;

	internal MongoDBClient SnapshotClient { get; private set; } = default!;

	public MongoDBEventStore<TAggregate> CreateEventStore<TAggregate>(
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
		MongoDBEventStore<TAggregate> EventStore,
		IMongoDBEventStoreTelemetry Telemetry,
		IDistributedCache Cache,
		MongoDBClient EventClient,
		MongoDBClient SnapshotClient
	) CreateEventStoreContext<TAggregate>(
		IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
		bool removeFromCacheOnDelete = false,
		int snapshotRecalculationInterval = 1
	)
		where TAggregate : class, IAggregate, new()
	{
		var runId = Guid.NewGuid();

		var cache = CreateDistributedCache();
		Cache = cache;

		var telemetry = Substitute.For<IMongoDBEventStoreTelemetry>();
		Telemetry = telemetry;

		_eventNameMapper = new AggregateEventNameMapper();

		var connectionString = _mongoDBContainer.GetConnectionString();

		var aggregateRequirementsManager = Substitute.For<IAggregateRequirementsManager>();
		MongoDBEventStoreOptions mongoDBOptions = new()
		{
			ApplicationName = nameof(MongoDBEventStoreFixture),
			ConnectionString = connectionString,
			Database = $"TestDatabase_{runId}",
			EventCollection = $"TestCollection_Events_{runId}",
			SnapshotCollection = $"TestCollection_Snapshots_{runId}",
			ReplicaName = "rs0",
			TimeoutInSeconds = 60,
			RemoveDeletedFromCache = removeFromCacheOnDelete,
			SnapshotInterval = snapshotRecalculationInterval,
		};

		var mongoDBClientTelemetry = Substitute.For<IMongoDBClientTelemetry>();
		MongoDBEventStore<TAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			mongoDbOptions: Microsoft.Extensions.Options.Options.Create(mongoDBOptions),
			distributedCache: cache,
			aggregateChangeNotifier: aggregateChangeNotifier
				?? Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>(),
			eventStoreTelemetry: telemetry,
			mongoDBClientTelemetry: mongoDBClientTelemetry,
			aggregateRequirementsManager: aggregateRequirementsManager
		);

		var eventClient = new MongoDBClient(
			mongoDBClientTelemetry,
			new() { ConnectionString = mongoDBOptions.ConnectionString, ReplicaName = mongoDBOptions.ReplicaName },
			mongoDBOptions.Database,
			mongoDBOptions.EventCollection
		);
		EventClient = eventClient;

		var snapshotClient = new MongoDBClient(
			mongoDBClientTelemetry,
			new() { ConnectionString = mongoDBOptions.ConnectionString, ReplicaName = mongoDBOptions.ReplicaName },
			mongoDBOptions.Database,
			mongoDBOptions.SnapshotCollection
		);
		SnapshotClient = snapshotClient;

		return (eventStore, telemetry, cache, eventClient, snapshotClient);
	}

	public static IDistributedCache CreateDistributedCache()
	{
		var cache = Substitute.For<IDistributedCache>();
		cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNullForAnyArgs();

		return cache;
	}

	public async Task InitializeAsync()
	{
		await _mongoDBContainer.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		EventClient?.Dispose();
		SnapshotClient?.Dispose();

		await _mongoDBContainer.DisposeAsync();
	}
}
