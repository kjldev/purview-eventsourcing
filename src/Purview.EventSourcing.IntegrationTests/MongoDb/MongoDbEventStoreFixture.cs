using Microsoft.Extensions.Caching.Distributed;
using NSubstitute.ReturnsExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.MongoDB.StorageClients;
using Purview.EventSourcing.Services;
using Testcontainers.MongoDb;

namespace Purview.EventSourcing.MongoDB;

public sealed class MongoDBEventStoreFixture : IAsyncLifetime
{
	readonly MongoDbContainer _mongoDBContainer;

	IAggregateEventNameMapper _eventNameMapper = default!;
	IDisposable? _eventStoreAsDisposable;

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
		int correlationIdsToGenerate = 1,
		bool removeFromCacheOnDelete = false,
		int snapshotRecalculationInterval = 1)
		where TAggregate : class, IAggregate, new()
	{
		var runId = Guid.NewGuid();
		var runIds = Enumerable.Range(1, correlationIdsToGenerate)
			.Select(_ => $"{Guid.NewGuid()}".ToUpperInvariant())
			.ToArray();

		Cache = CreateDistributedCache();
		Telemetry = Substitute.For<IMongoDBEventStoreTelemetry>();

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
			SnapshotInterval = snapshotRecalculationInterval
		};

		var mongoDBClientTelemetry = Substitute.For<IMongoDBClientTelemetry>();
		MongoDBEventStore<TAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			mongoDbOptions: Microsoft.Extensions.Options.Options.Create(mongoDBOptions),
			distributedCache: Cache,
			aggregateChangeNotifier: aggregateChangeNotifier ?? Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>(),
			eventStoreTelemetry: Telemetry,
			mongoDBClientTelemetry: mongoDBClientTelemetry,
			aggregateRequirementsManager: aggregateRequirementsManager
		);

		EventClient = new(
			mongoDBClientTelemetry,
			new()
			{
				ConnectionString = mongoDBOptions.ConnectionString,
				ReplicaName = mongoDBOptions.ReplicaName
			}, mongoDBOptions.Database, mongoDBOptions.EventCollection);
		SnapshotClient = new(
			mongoDBClientTelemetry,
			new()
			{
				ConnectionString = mongoDBOptions.ConnectionString,
				ReplicaName = mongoDBOptions.ReplicaName
			}, mongoDBOptions.Database, mongoDBOptions.SnapshotCollection);

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

	public async Task InitializeAsync()
	{
		await _mongoDBContainer.StartAsync();
	}

	public async Task DisposeAsync()
	{
		_eventStoreAsDisposable?.Dispose();

		await _mongoDBContainer.DisposeAsync();
	}
}
