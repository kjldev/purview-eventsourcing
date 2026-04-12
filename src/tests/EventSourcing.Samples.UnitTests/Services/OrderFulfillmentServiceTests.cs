using FluentValidation.Results;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Services;

namespace Purview.EventSourcing.Samples.UnitTests.Services;

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
		i.Initialize("widget-sku", "Widget", initialQuantity: quantity);
		return i;
	}

	static OrderAggregate NewOrder(string? id = null)
	{
		var o = new OrderAggregate();
		o.Details.Id = id ?? Guid.NewGuid().ToString("N");
		return o;
	}

	static SaveResult<T> SuccessResult<T>(T aggregate) where T : class, IAggregate, new() =>
		new(aggregate, new ValidationResult(), saved: true, skipped: false);

	static SaveResult<T> FailResult<T>(T aggregate) where T : class, IAggregate, new() =>
		new(aggregate, new ValidationResult([new ValidationFailure("field", "Save failed")]), saved: false, skipped: false);

	OrderFulfillmentService CreateService(
		IQueryableEventStore<OrderAggregate>? orderStore = null,
		IQueryableEventStore<InventoryAggregate>? inventoryStore = null,
		IQueryableEventStore<CustomerAggregate>? customerStore = null
	) => new(
		orderStore ?? Substitute.For<IQueryableEventStore<OrderAggregate>>(),
		inventoryStore ?? Substitute.For<IQueryableEventStore<InventoryAggregate>>(),
		customerStore ?? Substitute.For<IQueryableEventStore<CustomerAggregate>>()
	);

	[Test]
	public async Task PlaceOrderAsync_GivenNullCustomer_ReturnsFail(CancellationToken cancellationToken)
	{
		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync("missing", null, cancellationToken).Returns((CustomerAggregate?)null);

		var result = await CreateService(customerStore: customerStore)
			.PlaceOrderAsync("missing", "inv-1", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).IsNotNullOrEmpty();
	}

	[Test]
	public async Task PlaceOrderAsync_GivenInactiveCustomer_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		customer.Deactivate();

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var result = await CreateService(customerStore: customerStore)
			.PlaceOrderAsync(customer.Id(), "inv-1", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains(customer.Name);
	}

	[Test]
	public async Task PlaceOrderAsync_GivenNullInventory_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync("missing-inv", null, cancellationToken).Returns((InventoryAggregate?)null);

		var result = await CreateService(customerStore: customerStore, inventoryStore: inventoryStore)
			.PlaceOrderAsync(customer.Id(), "missing-inv", 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).IsNotNullOrEmpty();
	}

	[Test]
	public async Task PlaceOrderAsync_GivenInsufficientStock_ReturnsFail(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 5);

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync(inventory.Id(), null, cancellationToken).Returns(inventory);

		var result = await CreateService(customerStore: customerStore, inventoryStore: inventoryStore)
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

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync(inventory.Id(), null, cancellationToken).Returns(inventory);
		inventoryStore.SaveAsync(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(SuccessResult(inventory));

		var orderStore = Substitute.For<IQueryableEventStore<OrderAggregate>>();
		orderStore.CreateAsync(null, cancellationToken).Returns(order);
		orderStore.SaveAsync(Arg.Any<OrderAggregate>(), null, cancellationToken)
			.Returns(SuccessResult(order));

		var result = await CreateService(orderStore, inventoryStore, customerStore)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 3, "123 Main St", cancellationToken);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(result.Order).IsNotNull();
		await Assert.That(result.Inventory).IsNotNull();
	}

	[Test]
	public async Task PlaceOrderAsync_GivenValidData_OrderHasLineItemAndIsConfirmed(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 20);
		var order = NewOrder();

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync(inventory.Id(), null, cancellationToken).Returns(inventory);
		inventoryStore.SaveAsync(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(SuccessResult(inventory));

		var orderStore = Substitute.For<IQueryableEventStore<OrderAggregate>>();
		orderStore.CreateAsync(null, cancellationToken).Returns(order);
		orderStore.SaveAsync(Arg.Any<OrderAggregate>(), null, cancellationToken)
			.Returns(callInfo => SuccessResult((OrderAggregate)callInfo[0]));

		await CreateService(orderStore, inventoryStore, customerStore)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 2, null, cancellationToken);

		await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
		await Assert.That(order.LineItems).Count().IsEqualTo(1);
		await Assert.That(order.CustomerId).IsEqualTo(customer.Id());
		await Assert.That(order.TotalAmount).IsGreaterThan(0m);
	}

	[Test]
	public async Task PlaceOrderAsync_WhenOrderSaveFails_ReturnsFailWithoutSavingInventory(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 50);
		var order = NewOrder();

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync(inventory.Id(), null, cancellationToken).Returns(inventory);

		var orderStore = Substitute.For<IQueryableEventStore<OrderAggregate>>();
		orderStore.CreateAsync(null, cancellationToken).Returns(order);
		orderStore.SaveAsync(Arg.Any<OrderAggregate>(), null, cancellationToken)
			.Returns(FailResult(order));

		var result = await CreateService(orderStore, inventoryStore, customerStore)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();

		// Inventory should never have been saved — compensation not needed since order wasn't saved
		await inventoryStore.DidNotReceive().SaveAsync(Arg.Any<InventoryAggregate>(), null, cancellationToken);
	}

	[Test]
	public async Task PlaceOrderAsync_WhenInventorySaveFails_CompensatesWithOrderCancellation(CancellationToken cancellationToken)
	{
		var customer = ActiveCustomer();
		var inventory = StockedInventory(quantity: 50);
		var order = NewOrder();

		var customerStore = Substitute.For<IQueryableEventStore<CustomerAggregate>>();
		customerStore.GetAsync(customer.Id(), null, cancellationToken).Returns(customer);

		var inventoryStore = Substitute.For<IQueryableEventStore<InventoryAggregate>>();
		inventoryStore.GetAsync(inventory.Id(), null, cancellationToken).Returns(inventory);
		inventoryStore.SaveAsync(Arg.Any<InventoryAggregate>(), null, cancellationToken)
			.Returns(FailResult(inventory));

		var orderStore = Substitute.For<IQueryableEventStore<OrderAggregate>>();
		orderStore.CreateAsync(null, cancellationToken).Returns(order);
		orderStore.SaveAsync(Arg.Any<OrderAggregate>(), null, cancellationToken)
			.Returns(SuccessResult(order));

		var result = await CreateService(orderStore, inventoryStore, customerStore)
			.PlaceOrderAsync(customer.Id(), inventory.Id(), quantity: 1, null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("cancelled");

		// Order.SaveAsync called twice: initial save + compensation cancel save
		await orderStore.Received(2).SaveAsync(Arg.Any<OrderAggregate>(), null, cancellationToken);
		await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
	}
}

