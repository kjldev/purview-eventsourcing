using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures.SqlServer;

namespace Purview.EventSourcing.SqlServer.Snapshots;

// Holds the fixture and shared helpers for SQL Server-specific snapshot tests (index creation,
// unsupported payloads). The provider-agnostic snapshot contract suite lives in
// SnapshotStoreContractTests.
[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerTestSession)]
public partial class SQLServerSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture)
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new() { Details = { Id = id ?? $"{Guid.NewGuid():D}" } };

		action?.Invoke(aggregate);

		return aggregate;
	}
}
