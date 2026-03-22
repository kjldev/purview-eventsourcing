using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures;

namespace Purview.EventSourcing.SqlServer.Snapshot;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public partial class SqlServerSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture)
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new() { Details = { Id = id ?? Guid.NewGuid().ToString() } };

		action?.Invoke(aggregate);

		return aggregate;
	}
}
