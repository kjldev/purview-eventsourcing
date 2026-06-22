using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures.CosmosDb;

namespace Purview.EventSourcing.CosmosDb.Snapshots;

[ClassDataSource<CosmosDbSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
[NotInParallel(nameof(CosmosDbClient))]
public partial class CosmosDbSnapshotEventStoreTests(CosmosDbSnapshotEventStoreFixture fixture)
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new() { Details = { Id = id ?? Guid.NewGuid().ToString() } };

		action?.Invoke(aggregate);

		return aggregate;
	}
}
