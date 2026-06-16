using Microsoft.Extensions.Options;
using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer.Snapshots;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class OrderAggregateSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture)
{
	[Test]
	public async Task SnapshotAsync_GivenOrderAggregateWithLineItems_QueriesAndRestoresLineItems(
		CancellationToken cancellationToken
	)
	{
		var context = fixture.CreateContext(tableName: $"Snapshots_{Guid.NewGuid():N}");
		var id = Guid.NewGuid().ToString("D");

		var aggregate = new OrderAggregate();
		aggregate.Details.Id = id;
		aggregate
			.CreateOrder("customer-1")
			.AddLineItem("prod-1", "Widget A", 2, 29.99m)
			.AddLineItem("prod-2", "Widget B", 1, 49.99m);

		var innerEventStore = Substitute.For<INonQueryableEventStore<OrderAggregate>>();
		innerEventStore
			.FulfilRequirements(Arg.Any<OrderAggregate>())
			.Returns(callInfo => callInfo.Arg<OrderAggregate>());

		var store = new SqlServerSnapshotEventStore<OrderAggregate>(
			innerEventStore,
			Options.Create(
				new SqlServerSnapshotEventStoreOptions
				{
					ConnectionString = context.SqlServerConnectionString,
					TableName = $"Snapshots_{Guid.NewGuid():N}",
					SchemaName = "dbo",
					AutoCreateTable = true,
				}
			),
			Substitute.For<ISqlServerSnapshotEventStoreTelemetry>()
		);

		await store.SnapshotAsync(aggregate, cancellationToken);

		var count = await store.CountAsync(m => m.CustomerId == "customer-1", cancellationToken);
		var query = await store.QueryAsync(m => m.CustomerId == "customer-1", cancellationToken: cancellationToken);
		var result = query.Results.SingleOrDefault();

		await Assert.That(count).IsEqualTo(1);
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.LineItems).Count().IsEqualTo(2);
		await Assert.That(result.LineItems.ElementAt(0).ProductId).IsEqualTo("prod-1");
		await Assert.That(result.LineItems.ElementAt(1).UnitPrice).IsEqualTo(49.99m);
		await Assert.That(result.TotalAmount).IsEqualTo(109.97m);
	}
}
