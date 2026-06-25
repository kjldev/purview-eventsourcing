using Microsoft.Extensions.Caching.Distributed;
using NSubstitute.ReturnsExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.Services;
using Purview.EventSourcing.SqlServer.Events;
using Testcontainers.MsSql;
using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.Fixtures.SqlServer;

public class SqlServerEventStoreFixture : IAsyncInitializer, IAsyncDisposable
{
	readonly MsSqlContainer _msSqlContainer = ContainerHelper.CreateMsSql();

	IAggregateEventNameMapper _eventNameMapper = default!;

	public SqlServerEventStoreFixture()
	{
		EventStoreOperationContext.RequiresValidPrincipalIdentifierDefault = false;
	}

	public IDistributedCache Cache { get; private set; } = default!;

	public ISqlServerEventStoreTelemetry Telemetry { get; private set; } = default!;

	internal SqlServerEventStoreClient Client { get; private set; } = default!;

	public string ConnectionString => _msSqlContainer.GetConnectionString();

	public SqlServerEventStore<TAggregate> CreateEventStore<TAggregate>(
		IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
		bool removeFromCacheOnDelete = false,
		Guid? runId = null
	)
		where TAggregate : class, IAggregate, new() =>
		CreateEventStoreContext(aggregateChangeNotifier, removeFromCacheOnDelete, runId).EventStore;

	internal (
		SqlServerEventStore<TAggregate> EventStore,
		SqlServerEventStoreClient Client,
		IDistributedCache Cache,
		ISqlServerEventStoreTelemetry Telemetry
	) CreateEventStoreContext<TAggregate>(
		IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
		bool removeFromCacheOnDelete = false,
		Guid? runId = null
	)
		where TAggregate : class, IAggregate, new()
	{
		runId ??= Guid.NewGuid();
		var cache = CreateDistributedCache();
		Cache = cache;
		var telemetry = Substitute.For<ISqlServerEventStoreTelemetry>();
		Telemetry = telemetry;
		_eventNameMapper = new AggregateEventNameMapper();

		var connectionString = _msSqlContainer.GetConnectionString();
		var aggregateRequirementsManager = Substitute.For<IAggregateRequirementsManager>();

		SqlServerEventStoreOptions options = new()
		{
			ConnectionString = connectionString,
			TableName = $"EventStoreEvents_{runId:N}",
			SchemaName = "dbo",
			AutoCreateTable = true,
			TimeoutInSeconds = 60,
			RemoveDeletedFromCache = removeFromCacheOnDelete,
		};

		var client = new SqlServerEventStoreClient(options);
		Client = client;

		SqlServerEventStore<TAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			sqlServerOptions: Microsoft.Extensions.Options.Options.Create(options),
			distributedCache: cache,
			eventStoreTelemetry: telemetry,
			aggregateChangeNotifier: aggregateChangeNotifier
				?? Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>(),
			aggregateRequirementsManager: aggregateRequirementsManager
		);

		return (eventStore, client, cache, telemetry);
	}

	public static IDistributedCache CreateDistributedCache()
	{
		var cache = Substitute.For<IDistributedCache>();
		cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNullForAnyArgs();
		return cache;
	}

	public async Task InitializeAsync()
	{
		await _msSqlContainer.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _msSqlContainer.DisposeAsync();
		GC.SuppressFinalize(this);
	}
}
