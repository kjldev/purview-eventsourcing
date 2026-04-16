using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Services;

namespace Purview.EventSourcing.Samples.Services;

public sealed class OrderFulfillmentServiceTests
{
	static CustomerAggregate ActiveCustomer(string id = "cust-1")
	{
		var c = new CustomerAggregate();
		c.Details.Id = id;
		c.RegisterCustomer("Alice Johnson", "alice@example.com");
		return c;
	}

	static InventoryAggregate StockedInventory(string id = "inv-1", int quantity = 100)
	{
		var i = new InventoryAggregate();
		i.Details.Id = id;
		i.Initialize("widget-sku", "Widget", "warehouse-1", "Main Warehouse", initialQuantity: quantity);
		return i;
	}

	static OrderAggregate NewOrder(string? id = null)
	{
		var o = new OrderAggregate();
		o.Details.Id = id ?? Guid.NewGuid().ToString("N");
		return o;
	}

	static TransactionResult SuccessfulTransaction(params IAggregate[] aggregates) =>
		new(
		[
			.. aggregates.Select(aggregate => new TransactionAggregateResult(aggregate, saved: true, skipped: false, error: null)),
		]
	);

	static TransactionResult FailedTransaction(IAggregate aggregate) =>
		new([new TransactionAggregateResult(aggregate, saved: false, skipped: false, error: new InvalidOperationException("Commit failed"))]);

	OrderFulfillmentService CreateService(
		IEventStoreTransactionFactory? transactionFactory = null,
		IQueryableEventStore? store = null
	) => new(transactionFactory ?? Substitute.For<IEventStoreTransactionFactory>(), store ?? Substitute.For<IQueryableEventStore>());

	[Test]
	public async Task PlaceOrderAsync_GivenNullCustomer_ReturnsFail(CancellationToken cancellationToken)
	{
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>("missing", null, cancellationToken).Returns((CustomerAggregate?)null);

		var result = await CreateService(store: store)
			.PlaceOrderAsync("missing", "inv-1", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).IsNotNullOrEmpty();
	}

	[Test]
	public async Task PlaceOrderAsync_GivenInactiveCustomer_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		customer.Deactivate();

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);

		var result = await CreateService(store: store)
			.PlaceOrderAsync(customer.Id(), "inv-1", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains(customer.Name);
	}

	[Test]
	public async Task PlaceOrderAsync_GivenNullInventory_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>("missing-inv", null, cancellationToken).Returns((InventoryAggregate?)null);

		var result = await CreateService(store: store)
			.PlaceOrderAsync(customer.Id(), "missing-inv", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).IsNotNullOrEmpty();
	}

	[Test]
	public async Task PlaceOrderAsync_GivenInsufficientStock_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 5);

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken).Returns(inventory);

		var result = await CreateService(store: store)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 10, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Insufficient stock");
	}

	[Test]
	public async Task PlaceOrderAsync_GivenValidData_ReturnsSuccess(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 50);
		var order = NewOrder();

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(SuccessfulTransaction(order, inventory));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken).Returns(inventory);
		store.CreateAsync<OrderAggregate>(null, cancellationToken).Returns(order);

		var result = await CreateService(transactionFactory, store)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 3, "123 Main St", cancellationToken);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(result.Order).IsNotNull();
		await Assert.That(result.Inventory).IsNotNull();
		transaction.Received(1).Enlist(order, (IEventStore)store, null);
		transaction.Received(1).Enlist(inventory, (IEventStore)store, null);
	}

	[Test]
	public async Task PlaceOrderAsync_GivenValidData_OrderHasLineItemAndIsConfirmed(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 20);
		var order = NewOrder();

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(SuccessfulTransaction(order, inventory));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken).Returns(inventory);
		store.CreateAsync<OrderAggregate>(null, cancellationToken).Returns(order);

		await CreateService(transactionFactory, store)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 2, null, cancellationToken);

		await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
		await Assert.That(order.LineItems).Count().IsEqualTo(1);
		await Assert.That(order.CustomerId).IsEqualTo(customer.Id());
		await Assert.That(order.TotalAmount).IsGreaterThan(0m);
	}

	[Test]
	public async Task PlaceOrderAsync_WhenTransactionCommitFails_ReturnsFailWithoutSuccess(
		CancellationToken cancellationToken
	)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 50);
		var order = NewOrder();

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(FailedTransaction(order));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken).Returns(inventory);
		store.CreateAsync<OrderAggregate>(null, cancellationToken).Returns(order);

		var result = await CreateService(transactionFactory, store)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Nothing was saved");
		await transaction.Received(1).CommitAsync(cancellationToken);
	}

	[Test]
	public async Task PlaceOrderAsync_WhenTransactionCommitFails_DoesNotCancelOrderInMemory(
		CancellationToken cancellationToken
	)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 50);
		var order = NewOrder();

		var transactionFactory = Substitute.For<IEventStoreTransactionFactory>();
		var transaction = Substitute.For<IEventStoreTransaction>();
		transactionFactory.Create(Arg.Any<string?>()).Returns(transaction);
		transaction.CommitAsync(cancellationToken).Returns(FailedTransaction(inventory));

		var store = Substitute.For<IQueryableEventStore>();
		store.GetAsync<CustomerAggregate>(customer.Id(), null, cancellationToken).Returns(customer);
		store.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken).Returns(inventory);
		store.CreateAsync<OrderAggregate>(null, cancellationToken).Returns(order);

		var result = await CreateService(transactionFactory, store)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Nothing was saved");
		await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
	}
}
