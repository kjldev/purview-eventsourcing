using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Snapshotting;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.SqlServer.Snapshot;
using Purview.EventSourcing.SqlServer.Snapshots;

namespace Purview.EventSourcing.Fixtures.SqlServer;

public class SqlServerSnapshotEventStoreFixture : SqlServerEventStoreFixture
{
	public SqlServerSnapshotEventStore<TAggregate> CreateSnapshotStore<TAggregate>(
		ISnapshotStrategy<TAggregate>? snapshotStrategy = null,
		ISnapshotStrategySelector? snapshotStrategySelector = null,
		IAggregateChangeFeedNotifier<TAggregate>? aggregateChangeNotifier = null,
		bool removeFromCacheOnDelete = false,
		Guid? runId = null
	)
		where TAggregate : class, IAggregate, new()
	{
		runId ??= Guid.NewGuid();
		var eventStore = CreateEventStore(aggregateChangeNotifier, removeFromCacheOnDelete, runId);
		SqlServerSnapshotEventStoreOptions config = new()
		{
			ConnectionString = ConnectionString,
			TableName = $"Snapshots_{runId:N}",
			SchemaName = "dbo",
			AutoCreateTable = true,
		};

		SqlServerSnapshotEventStore<TAggregate> snapshotStore = new(
			eventStore,
			Microsoft.Extensions.Options.Options.Create(config),
			Substitute.For<ISqlServerSnapshotEventStoreTelemetry>(),
			snapshotStrategy: snapshotStrategy,
			snapshotStrategySelector: snapshotStrategySelector
		);

		return snapshotStore;
	}
}
