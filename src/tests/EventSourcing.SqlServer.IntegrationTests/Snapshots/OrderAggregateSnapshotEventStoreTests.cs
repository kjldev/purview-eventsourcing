using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.SqlServer.Snapshots;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class OrderAggregateSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture)
{
	[Test]
	public async Task SnapshotAsync_GivenOrderAggregateWithLineItems_QueriesByLineItemCount(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<OrderAggregate>();
		var id = Guid.NewGuid().ToString("D");

		var aggregate = new OrderAggregate();
		aggregate.Details.Id = id;
		aggregate
			.CreateOrder("customer-1")
			.AddLineItem("prod-1", "Widget A", 2, 29.99m)
			.AddLineItem("prod-2", "Widget B", 1, 49.99m);

		await store.SnapshotAsync(aggregate, cancellationToken);

		var count = await store.CountAsync(
			m => m.CustomerId == "customer-1" && m.LineItems.Count == 2,
			cancellationToken
		);
		var result = await store.SingleOrDefaultAsync(
			m => m.CustomerId == "customer-1" && m.LineItems.Count == 2,
			cancellationToken: cancellationToken
		);

		await Assert.That(count).IsEqualTo(1);
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.LineItems).Count().IsEqualTo(2);
		await Assert.That(result.LineItems.ElementAt(0).ProductId).IsEqualTo("prod-1");
		await Assert.That(result.LineItems.ElementAt(1).UnitPrice).IsEqualTo(49.99m);
		await Assert.That(result.TotalAmount).IsEqualTo(109.97m);
	}

	[Test]
	public async Task SnapshotAsync_GivenOrderAggregateWithLineItems_QueriesAndRestoresLineItems(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<OrderAggregate>();
		var id = Guid.NewGuid().ToString("D");

		var aggregate = new OrderAggregate();
		aggregate.Details.Id = id;
		aggregate
			.CreateOrder("customer-1")
			.AddLineItem("prod-1", "Widget A", 2, 29.99m)
			.AddLineItem("prod-2", "Widget B", 1, 49.99m);

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
