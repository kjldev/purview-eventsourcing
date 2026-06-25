using System.Linq.Expressions;
using Purview.EventSourcing.Fixtures.SqlServer;

namespace Purview.EventSourcing.Samples.Domain;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class QueryableAggregateIntegrationTests(SqlServerSnapshotEventStoreFixture fixture)
{
	[Test]
	public async Task QueryAsync_GivenCustomerSelectorFiltersAndSort_ResultsMatchPageModelQuery(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<CustomerAggregate>();

		var alice = NewCustomer("Alice Able", "alice@example.com", isActive: true);
		var adam = NewCustomer("Adam Atlas", "adam@example.com", isActive: true);
		var zoe = NewCustomer("Zoe Zero", "zoe@example.com", isActive: false);

		await store.SnapshotAsync(alice, cancellationToken);
		await store.SnapshotAsync(adam, cancellationToken);
		await store.SnapshotAsync(zoe, cancellationToken);

		var search = "a";
		var showInactive = false;
		Expression<Func<CustomerAggregate, bool>> where = c =>
			(string.IsNullOrEmpty(search) || ((string)c.Name).Contains(search) || ((string)c.Email).Contains(search))
			&& (showInactive || c.IsActive);

		var result = await store.QueryAsync(
			where,
			q => q.OrderBy(c => c.Name),
			new ContinuationRequest { MaxRecords = 10, IncludeTotalCount = true },
			cancellationToken
		);

		await Assert.That(result.TotalCount).IsEqualTo(2);
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Name).IsEqualTo("Adam Atlas");
		await Assert.That(result.Results[1].Name).IsEqualTo("Alice Able");
	}

	[Test]
	public async Task QueryAsync_GivenBackOfficeCustomerSortModes_ResultsAreTranslatableAndOrdered(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<CustomerAggregate>();

		var lowEmail = NewCustomer("Carol C", "a@example.com", isActive: true);
		var highEmail = NewCustomer("Bob B", "z@example.com", isActive: false);

		await store.SnapshotAsync(lowEmail, cancellationToken);
		await store.SnapshotAsync(highEmail, cancellationToken);

		var byEmailDesc = await store.QueryAsync(
			_ => true,
			q => q.OrderByDescending(c => c.Email),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);
		await Assert.That(byEmailDesc.Results[0].Email).IsEqualTo("z@example.com");
		await Assert.That(byEmailDesc.Results[1].Email).IsEqualTo("a@example.com");

		var byStatusAsc = await store.QueryAsync(
			_ => true,
			q => q.OrderBy(c => c.IsActive),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);
		await Assert.That(byStatusAsc.Results[0].IsActive).IsFalse();
		await Assert.That(byStatusAsc.Results[1].IsActive).IsTrue();
	}

	[Test]
	public async Task QueryAsync_GivenBackOfficeStockFilterAndSort_ResultsMatchPageModelQuery(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<InventoryAggregate>();

		var highAvailable = NewInventory("prod-alpha", "Alpha Widget", quantityOnHand: 20, reserved: 2);
		var lowAvailable = NewInventory("prod-beta", "Beta Widget", quantityOnHand: 10, reserved: 6);
		var nonMatch = NewInventory("prod-gamma", "Gamma Gizmo", quantityOnHand: 50, reserved: 0);

		await store.SnapshotAsync(highAvailable, cancellationToken);
		await store.SnapshotAsync(lowAvailable, cancellationToken);
		await store.SnapshotAsync(nonMatch, cancellationToken);

		var search = "Widget";
		Expression<Func<InventoryAggregate, bool>> where = i =>
			i.ProductId.Contains(search) || i.ProductName.Contains(search);

		var result = await store.QueryAsync(
			where,
			q => q.OrderByDescending(i => i.AvailableQuantity),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);

		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].ProductId).IsEqualTo("prod-alpha");
		await Assert.That(result.Results[1].ProductId).IsEqualTo("prod-beta");
	}

	[Test]
	public async Task ListAsync_GivenBackOfficeStockSortByAvailableDesc_ResultsAreTranslatableAndOrdered(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<InventoryAggregate>();

		var highAvailable = NewInventory("prod-alpha", "Alpha Widget", quantityOnHand: 20, reserved: 2);
		var lowAvailable = NewInventory("prod-beta", "Beta Widget", quantityOnHand: 10, reserved: 6);

		await store.SnapshotAsync(highAvailable, cancellationToken);
		await store.SnapshotAsync(lowAvailable, cancellationToken);

		var result = await store.ListAsync(
			q => q.OrderByDescending(i => i.AvailableQuantity),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);

		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].ProductId).IsEqualTo("prod-alpha");
		await Assert.That(result.Results[1].ProductId).IsEqualTo("prod-beta");
	}

	[Test]
	public async Task QueryAsync_GivenBackOfficeCatalogInvariantCaseFilter_CountAndQueryAreTranslatable(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<InventoryAggregate>();

		var first = NewInventory("PROD-ALPHA", "Alpha Widget", quantityOnHand: 20, reserved: 2);
		var second = NewInventory("prod-beta", "beta widget", quantityOnHand: 10, reserved: 6);
		var nonMatch = NewInventory("prod-gamma", "Gamma Gizmo", quantityOnHand: 50, reserved: 0);

		await store.SnapshotAsync(first, cancellationToken);
		await store.SnapshotAsync(second, cancellationToken);
		await store.SnapshotAsync(nonMatch, cancellationToken);

#pragma warning disable CA1862 // intentional: mirror page model query shape
#pragma warning disable CA1308 // intentional: mirror page model query shape
		var search = "widget".ToLowerInvariant();
		Expression<Func<InventoryAggregate, bool>> where = i =>
			i.ProductId.ToLowerInvariant().Contains(search) || i.ProductName.ToLowerInvariant().Contains(search);
#pragma warning restore CA1308
#pragma warning restore CA1862

		var count = await store.CountAsync(where, cancellationToken);
		var result = await store.QueryAsync(
			where,
			q => q.OrderBy(i => i.ProductName),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);

		await Assert.That(count).IsEqualTo(2);
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].ProductName).IsEqualTo("Alpha Widget");
		await Assert.That(result.Results[1].ProductName).IsEqualTo("beta widget");
	}

	[Test]
	public async Task QueryAsync_GivenCustomerOrdersBySavedVersion_ResultsArePagedAndOrdered(
		CancellationToken cancellationToken
	)
	{
		var store = fixture.CreateSnapshotStore<OrderAggregate>();
		var selectedCustomerId = $"customer-{Guid.NewGuid():N}";
		var otherCustomerId = $"customer-{Guid.NewGuid():N}";

		var newest = NewOrder(selectedCustomerId, "order-new");
		newest.SetShippingAddress("A").ConfirmOrder();
		newest.Details.SavedVersion = newest.Details.CurrentVersion;
		await store.SnapshotAsync(newest, cancellationToken); // saved version 4

		var older = NewOrder(selectedCustomerId, "order-old");
		older.Details.SavedVersion = older.Details.CurrentVersion;
		await store.SnapshotAsync(older, cancellationToken); // saved version 2

		var other = NewOrder(otherCustomerId, "order-other");
		other.SetShippingAddress("B").ConfirmOrder().ShipOrder();
		other.Details.SavedVersion = other.Details.CurrentVersion;
		await store.SnapshotAsync(other, cancellationToken); // different customer

		Expression<Func<OrderAggregate, bool>> where = o => o.CustomerId == selectedCustomerId;

		var totalCount = await store.CountAsync(where, cancellationToken);
		var page1 = await store.QueryAsync(
			where,
			q => q.OrderByDescending(o => o.Details.SavedVersion),
			new ContinuationRequest { MaxRecords = 1, ContinuationToken = null },
			cancellationToken
		);
		var page2 = await store.QueryAsync(
			where,
			q => q.OrderByDescending(o => o.Details.SavedVersion),
			new ContinuationRequest { MaxRecords = 1, ContinuationToken = "1" },
			cancellationToken
		);

		await Assert.That(totalCount).IsEqualTo(2);
		await Assert.That(page1.Results).Count().IsEqualTo(1);
		await Assert.That(page2.Results).Count().IsEqualTo(1);
		await Assert.That(page1.Results[0].Details.SavedVersion).IsGreaterThan(page2.Results[0].Details.SavedVersion);
		await Assert.That(page1.Results[0].CustomerId).IsEqualTo(selectedCustomerId);
		await Assert.That(page2.Results[0].CustomerId).IsEqualTo(selectedCustomerId);
	}

	static CustomerAggregate NewCustomer(string name, string email, bool isActive)
	{
		var customer = new CustomerAggregate();
		customer.Details.Id = $"{Guid.NewGuid()}";
		customer.RegisterCustomer(name, email);
		if (!isActive)
			customer.Deactivate();

		return customer;
	}

	static InventoryAggregate NewInventory(string productId, string productName, int quantityOnHand, int reserved)
	{
		var inventory = new InventoryAggregate();
		inventory.Details.Id = $"{Guid.NewGuid()}";
		inventory.Create(productId, productName, "loc-1", "Main", initialQuantity: quantityOnHand);
		if (reserved > 0)
			inventory.ReserveStock(reserved, $"order-{Guid.NewGuid():N}");

		return inventory;
	}

	static OrderAggregate NewOrder(string customerId, string orderIdSuffix)
	{
		var order = new OrderAggregate();
		order.Details.Id = $"order-{orderIdSuffix}-{Guid.NewGuid():N}";
		order.CreateOrder(customerId).AddLineItem("prod-1", "Widget", 1, 10m);
		return order;
	}
}
