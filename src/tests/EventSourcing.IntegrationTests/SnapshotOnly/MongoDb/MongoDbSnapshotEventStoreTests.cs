using MongoDB.Driver;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures;

namespace Purview.EventSourcing.MongoDB.Snapshot;

[ClassDataSource<MongoDBSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public partial class MongoDBSnapshotEventStoreTests(MongoDBSnapshotEventStoreFixture fixture)
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new() { Details = { Id = id ?? Guid.NewGuid().ToString() } };

		action?.Invoke(aggregate);

		return aggregate;
	}

	static FilterDefinition<PersistenceAggregate> PredicateId(string aggregateId)
	{
		var builder = new FilterDefinitionBuilder<PersistenceAggregate>().Eq("_id", aggregateId);

		return builder;
	}
}
