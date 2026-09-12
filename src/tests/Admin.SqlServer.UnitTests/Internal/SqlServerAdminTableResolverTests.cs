using Purview.EventSourcing.SqlServer.Events;

namespace Purview.EventSourcing.Admin.SQLServer.Internal;

public sealed class SqlServerAdminTableResolverTests
{
	[Test]
	public async Task ResolveTables_ReturnsDefaultTable_WhenAggregateTypeIsNotSpecified()
	{
		SqlServerEventStoreOptions options = new()
		{
			ConnectionString = "Server=.;Database=Db;Trusted_Connection=True;",
			SchemaName = "dbo",
			TableName = "EventStoreEvents",
		};

		var tables = SqlServerAdminTableResolver.ResolveTables(options, null);

		await Assert.That(tables.Count).IsEqualTo(1);
		await Assert.That(tables[0].SchemaName).IsEqualTo("dbo");
		await Assert.That(tables[0].TableName).IsEqualTo("EventStoreEvents");
	}

	[Test]
	public async Task ResolveTable_UsesAggregateOverride_WhenConfigured()
	{
		SqlServerEventStoreOptions options = new()
		{
			ConnectionString = "Server=.;Database=Db;Trusted_Connection=True;",
			SchemaName = "dbo",
			TableName = "EventStoreEvents",
			AggregateTableOverrides =
			{
				["Order"] = new SqlServerAggregateTableOverride { SchemaName = "orders", TableName = "OrderEvents" },
			},
		};

		var table = SqlServerAdminTableResolver.ResolveTable(options, "Order");

		await Assert.That(table.AggregateTypeFilter).IsEqualTo("Order");
		await Assert.That(table.SchemaName).IsEqualTo("orders");
		await Assert.That(table.TableName).IsEqualTo("OrderEvents");
	}
}
