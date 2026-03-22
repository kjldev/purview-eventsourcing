using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.UnitTests.Domain;

public class InventoryAggregateTests
{
	static InventoryAggregate CreateInventory(string? id = null, int initialQty = 100)
	{
		var inv = new InventoryAggregate();
		if (id is not null)
			inv.Details.Id = id;
		inv.Initialize("prod-1", "Widget A", initialQty);
		return inv;
	}

	#region Initialize Tests

	[Test]
	public async Task Initialize_GivenValidData_SetsProperties(CancellationToken cancellationToken)
	{
		// Arrange & Act
		var inv = CreateInventory("inv-1", initialQty: 50);

		// Assert
		await Assert.That(inv.ProductId).IsEqualTo("prod-1");
		await Assert.That(inv.ProductName).IsEqualTo("Widget A");
		await Assert.That(inv.QuantityOnHand).IsEqualTo(50);
		await Assert.That(inv.ReservedQuantity).IsEqualTo(0);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(50);
	}

	[Test]
	public void Initialize_GivenNullProductId_ThrowsArgumentException()
	{
		var inv = new InventoryAggregate();
		Assert.Throws<ArgumentException>(() => inv.Initialize(null!, "name"));
	}

	[Test]
	public void Initialize_GivenNegativeQuantity_ThrowsArgumentOutOfRangeException()
	{
		var inv = new InventoryAggregate();
		Assert.Throws<ArgumentOutOfRangeException>(() => inv.Initialize("p1", "name", -1));
	}

	#endregion

	#region ReceiveStock Tests

	[Test]
	public async Task ReceiveStock_GivenPositiveQuantity_IncreasesOnHand(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 50);
		inv.ReceiveStock(25);
		await Assert.That(inv.QuantityOnHand).IsEqualTo(75);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(75);
	}

	[Test]
	public void ReceiveStock_GivenZeroQuantity_ThrowsArgumentOutOfRangeException()
	{
		var inv = CreateInventory("inv-1");
		Assert.Throws<ArgumentOutOfRangeException>(() => inv.ReceiveStock(0));
	}

	#endregion

	#region ReserveStock Tests

	[Test]
	public async Task ReserveStock_GivenAvailableQuantity_ReservesSuccessfully(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(10, "order-1");

		await Assert.That(inv.ReservedQuantity).IsEqualTo(10);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(90);
		await Assert.That(inv.QuantityOnHand).IsEqualTo(100); // unchanged
	}

	[Test]
	public void ReserveStock_GivenInsufficientStock_ThrowsInvalidOperationException()
	{
		var inv = CreateInventory("inv-1", initialQty: 5);
		Assert.Throws<InvalidOperationException>(() => inv.ReserveStock(10, "order-1"));
	}

	[Test]
	public async Task ReserveStock_GivenMultipleReservations_AccumulatesReserved(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(10, "order-1");
		inv.ReserveStock(20, "order-2");

		await Assert.That(inv.ReservedQuantity).IsEqualTo(30);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(70);
	}

	#endregion

	#region ShipStock Tests

	[Test]
	public async Task ShipStock_GivenReservedQuantity_DeductsFromBoth(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(10, "order-1");
		inv.ShipStock(10, "order-1");

		await Assert.That(inv.QuantityOnHand).IsEqualTo(90);
		await Assert.That(inv.ReservedQuantity).IsEqualTo(0);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(90);
	}

	[Test]
	public void ShipStock_GivenMoreThanReserved_ThrowsInvalidOperationException()
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(5, "order-1");
		Assert.Throws<InvalidOperationException>(() => inv.ShipStock(10, "order-1"));
	}

	#endregion

	#region ReleaseReservation Tests

	[Test]
	public async Task ReleaseReservation_GivenReservedQuantity_ReleasesCorrectly(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(10, "order-1");
		inv.ReleaseReservation(10, "order-1");

		await Assert.That(inv.ReservedQuantity).IsEqualTo(0);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(100);
	}

	[Test]
	public void ReleaseReservation_GivenMoreThanReserved_ThrowsInvalidOperationException()
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(5, "order-1");
		Assert.Throws<InvalidOperationException>(() => inv.ReleaseReservation(10, "order-1"));
	}

	#endregion

	#region AdjustStock Tests

	[Test]
	public async Task AdjustStock_GivenNewQuantity_SetsQuantityDirectly(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.AdjustStock(50, "Physical count correction");

		await Assert.That(inv.QuantityOnHand).IsEqualTo(50);
	}

	[Test]
	public async Task AdjustStock_GivenQuantityBelowReserved_CapsReservedQuantity(CancellationToken cancellationToken)
	{
		var inv = CreateInventory("inv-1", initialQty: 100);
		inv.ReserveStock(20, "order-1");
		inv.AdjustStock(10, "Damage");

		await Assert.That(inv.QuantityOnHand).IsEqualTo(10);
		await Assert.That(inv.ReservedQuantity).IsEqualTo(10); // capped
		await Assert.That(inv.AvailableQuantity).IsEqualTo(0);
	}

	#endregion

	#region Multi-Aggregate Workflow Test

	[Test]
	public async Task FullOrderFulfillmentWorkflow_TracksCorrectState(CancellationToken cancellationToken)
	{
		// Simulate order fulfillment across inventory
		var inv = CreateInventory("inv-1", initialQty: 100);

		// Reserve for order
		inv.ReserveStock(5, "order-1");
		await Assert.That(inv.AvailableQuantity).IsEqualTo(95);

		// Ship order
		inv.ShipStock(5, "order-1");
		await Assert.That(inv.QuantityOnHand).IsEqualTo(95);
		await Assert.That(inv.ReservedQuantity).IsEqualTo(0);

		// Receive restock
		inv.ReceiveStock(200);
		await Assert.That(inv.QuantityOnHand).IsEqualTo(295);
		await Assert.That(inv.AvailableQuantity).IsEqualTo(295);

		// Total events: init + reserve + ship + receive = 4
		await Assert.That(inv.GetUnsavedEvents().Count()).IsEqualTo(4);
	}

	#endregion
}
