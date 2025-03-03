using MongoDB.Driver;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.SnapshotOnly.MongoDB;

namespace Purview.EventSourcing.MongoDB.Snapshot;

[Collection("MongoDB")]
[NCrunch.Framework.Category("MongoDB")]
[NCrunch.Framework.Category("Storage")]
public partial class MongoDBSnapshotEventStoreTests(MongoDBSnapshotEventStoreFixture fixture) : IClassFixture<MongoDBSnapshotEventStoreFixture>
{
	static PersistenceAggregate CreateAggregate(string? id = null, Action<PersistenceAggregate>? action = null)
	{
		PersistenceAggregate aggregate = new()
		{
			Details =
			{
				Id = id ?? Guid.NewGuid().ToString()
			}
		};

		action?.Invoke(aggregate);

		return aggregate;
	}

	static FilterDefinition<PersistenceAggregate> PredicateId(string aggregateId)
	{
		var builder = new FilterDefinitionBuilder<PersistenceAggregate>()
			.Eq("_id", aggregateId);

		return builder;
	}
}
