using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.SnapshotOnly.SqlServer;

namespace Purview.EventSourcing.SqlServer.Snapshot;

[Collection("SqlServer")]
[NCrunch.Framework.Category("SqlServer")]
[NCrunch.Framework.Category("Storage")]
[NCrunch.Framework.Serial]
[CollectionDefinition("SqlServer")]
public partial class SqlServerSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture) : IClassFixture<SqlServerSnapshotEventStoreFixture>
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
