using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Tests demonstrating cross-aggregate business workflows.
/// </summary>
public class MultiAggregateWorkflowTests
{
	[Test]
	public async Task OrderFulfillment_CustomerPlacesOrderWithInventoryReservation_AllAggregatesHaveCorrectState()
	{
		// Arrange — set up customer, inventory, and order
		var customer = new CustomerAggregate();
		customer.Details.Id = "cust-1";
		customer.RegisterCustomer("Alice Johnson", "alice@example.com");

		var inventory = new InventoryAggregate();
		inventory.Details.Id = "inv-widget";
		inventory.Initialize("widget-1", "Premium Widget", "warehouse-1", "Main Warehouse", initialQuantity: 100);

		var order = new OrderAggregate();
		order.Details.Id = "order-1";

		// Act — full order lifecycle
		order.CreateOrder(customer.Id());
		order.AddLineItem("widget-1", "Premium Widget", 5, 19.99m);
		order.SetShippingAddress("789 Commerce Blvd");

		inventory.ReserveStock(5, order.Id());
		order.ConfirmOrder();
		order.ShipOrder();
		inventory.ShipStock(5, order.Id());
		order.CompleteOrder();

		// Assert — final state
		await Assert.That(customer.IsActive).IsTrue();
		await Assert.That(customer.Name).IsEqualTo("Alice Johnson");

		await Assert.That(order.Status).IsEqualTo(OrderStatus.Completed);
		await Assert.That(order.TotalAmount).IsEqualTo(99.95m);
		await Assert.That(order.LineItems).Count().IsEqualTo(1);

		await Assert.That(inventory.QuantityOnHand).IsEqualTo(95);
		await Assert.That(inventory.ReservedQuantity).IsEqualTo(0);
		await Assert.That(inventory.AvailableQuantity).IsEqualTo(95);
	}

	[Test]
	public async Task OrderCancellation_ReleasesInventoryReservation_InventoryRestored()
	{
		// Arrange
		var inventory = new InventoryAggregate();
		inventory.Details.Id = "inv-gadget";
		inventory.Initialize("gadget-1", "Gadget", "warehouse-1", "Main Warehouse", initialQuantity: 50);

		var order = new OrderAggregate();
		order.Details.Id = "order-cancel";
		order.CreateOrder("cust-1").AddLineItem("gadget-1", "Gadget", 10, 15.00m);

		inventory.ReserveStock(10, order.Id());
		order.ConfirmOrder();

		// Act — cancel order and release reservation
		order.CancelOrder();
		inventory.ReleaseReservation(10, order.Id());

		// Assert
		await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
		await Assert.That(inventory.QuantityOnHand).IsEqualTo(50);
		await Assert.That(inventory.ReservedQuantity).IsEqualTo(0);
		await Assert.That(inventory.AvailableQuantity).IsEqualTo(50);
	}

	[Test]
	public async Task CustomerDeactivation_DoesNotAffectExistingOrders()
	{
		// Arrange
		var customer = new CustomerAggregate();
		customer.Details.Id = "cust-deactivate";
		customer.RegisterCustomer("Bob Smith", "bob@example.com");

		var order = new OrderAggregate();
		order.Details.Id = "order-existing";
		order.CreateOrder(customer.Id());
		order.AddLineItem("prod-1", "Item", 1, 10m);
		order.SetShippingAddress("456 Lane");
		order.ConfirmOrder();

		// Act — deactivate customer after order is confirmed
		customer.Deactivate();

		// Assert — order is still valid
		await Assert.That(customer.IsActive).IsFalse();
		await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);
		await Assert.That(order.CustomerId).IsEqualTo(customer.Id());
	}
}
