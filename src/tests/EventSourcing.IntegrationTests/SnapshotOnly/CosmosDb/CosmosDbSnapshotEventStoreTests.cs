using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

[ClassDataSource<CosmosDbSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
[NotInParallel(nameof(CosmosDbClient))]
[Skip("CosmosDB tests are disabled.")]
public partial class CosmosDbSnapshotEventStoreTests(CosmosDbSnapshotEventStoreFixture fixture)
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new() { Details = { Id = id ?? Guid.NewGuid().ToString() } };

		action?.Invoke(aggregate);

		return aggregate;
	}
}
