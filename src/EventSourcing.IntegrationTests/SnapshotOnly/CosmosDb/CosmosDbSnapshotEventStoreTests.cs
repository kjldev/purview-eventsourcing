using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.SnapshotOnly.CosmosDb;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

[Collection("CosmosDb")]
[NCrunch.Framework.Category("CosmosDb")]
[NCrunch.Framework.Category("Storage")]
[NCrunch.Framework.Serial]
[CollectionDefinition("CosmosDb")]
public partial class CosmosDbSnapshotEventStoreTests(CosmosDbSnapshotEventStoreFixture fixture) : IClassFixture<CosmosDbSnapshotEventStoreFixture>
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
}
