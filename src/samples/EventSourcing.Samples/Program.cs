using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples;

static class Program
{
	static void Main()
	{
		Console.WriteLine("=== Purview EventSourcing Sample ===\n");

		DemoCustomerAggregate();
		DemoOrderAggregate();
		DemoInventoryAggregate();
		DemoMultiAggregateWorkflow();
	}

	static void DemoCustomerAggregate()
	{
		Console.WriteLine("--- Customer Aggregate (Simple) ---");

		var customer = new CustomerAggregate();
		customer.Details.Id = "customer-001";
		customer.RegisterCustomer("Jane Smith", "jane@example.com");

		Console.WriteLine($"Customer: {customer.Name}, Email: {customer.Email}, Active: {customer.IsActive}");
		Console.WriteLine($"Unsaved events: {customer.GetUnsavedEvents().Count()}");

		customer.ChangeEmail("jane.smith@newdomain.com");
		customer.ChangePhoneNumber("+1-555-0100");

		Console.WriteLine($"Updated Email: {customer.Email}, Phone: {customer.PhoneNumber}");
		Console.WriteLine($"Current Version: {customer.Details.CurrentVersion}");
		Console.WriteLine($"Total unsaved events: {customer.GetUnsavedEvents().Count()}\n");
	}

	static void DemoOrderAggregate()
	{
		Console.WriteLine("--- Order Aggregate (Complex) ---");

		var order = new OrderAggregate();
		order.Details.Id = "order-001";
		order.CreateOrder("customer-001");

		order.AddLineItem("prod-1", "Widget A", 2, 29.99m);
		order.AddLineItem("prod-2", "Widget B", 1, 49.99m);
		order.SetShippingAddress("123 Main St, City, ST 12345");

		Console.WriteLine($"Order Status: {order.Status}, Items: {order.LineItems.Count}, Total: ${order.TotalAmount}");

		order.ConfirmOrder();
		Console.WriteLine($"After confirm: Status = {order.Status}");

		order.ShipOrder();
		Console.WriteLine($"After ship: Status = {order.Status}, ShippedAt = {order.ShippedAt}");

		order.CompleteOrder();
		Console.WriteLine($"After complete: Status = {order.Status}, CompletedAt = {order.CompletedAt}");
		Console.WriteLine($"Total unsaved events: {order.GetUnsavedEvents().Count()}\n");
	}

	static void DemoInventoryAggregate()
	{
		Console.WriteLine("--- Inventory Aggregate (Stock Management) ---");

		var inventory = new InventoryAggregate();
		inventory.Details.Id = "inv-prod-1";
		inventory.Initialize("prod-1", "Widget A", initialQuantity: 100);

		Console.WriteLine($"Product: {inventory.ProductName}, On Hand: {inventory.QuantityOnHand}, Available: {inventory.AvailableQuantity}");

		inventory.ReserveStock(5, "order-001");
		Console.WriteLine($"After reserve 5: On Hand: {inventory.QuantityOnHand}, Reserved: {inventory.ReservedQuantity}, Available: {inventory.AvailableQuantity}");

		inventory.ShipStock(5, "order-001");
		Console.WriteLine($"After ship 5: On Hand: {inventory.QuantityOnHand}, Reserved: {inventory.ReservedQuantity}, Available: {inventory.AvailableQuantity}");

		inventory.ReceiveStock(50);
		Console.WriteLine($"After receive 50: On Hand: {inventory.QuantityOnHand}, Available: {inventory.AvailableQuantity}");
		Console.WriteLine($"Total unsaved events: {inventory.GetUnsavedEvents().Count()}\n");
	}

	static void DemoMultiAggregateWorkflow()
	{
		Console.WriteLine("--- Multi-Aggregate Workflow ---");
		Console.WriteLine("Scenario: Customer places order, inventory reserved, order shipped\n");

		// 1. Register customer
		var customer = new CustomerAggregate();
		customer.Details.Id = "customer-multi-001";
		customer.RegisterCustomer("Bob Builder", "bob@example.com");
		Console.WriteLine($"1. Customer registered: {customer.Name}");

		// 2. Initialize inventory
		var inventory = new InventoryAggregate();
		inventory.Details.Id = "inv-gadget";
		inventory.Initialize("gadget-1", "Super Gadget", initialQuantity: 50);
		Console.WriteLine($"2. Inventory initialized: {inventory.ProductName} ({inventory.QuantityOnHand} units)");

		// 3. Create and populate order
		var order = new OrderAggregate();
		order.Details.Id = "order-multi-001";
		order.CreateOrder(customer.Id());
		order.AddLineItem("gadget-1", "Super Gadget", 3, 79.99m);
		order.SetShippingAddress("456 Oak Ave, Town, ST 67890");
		Console.WriteLine($"3. Order created: {order.LineItems.Count} items, Total: ${order.TotalAmount}");

		// 4. Reserve inventory for the order
		inventory.ReserveStock(3, order.Id());
		Console.WriteLine($"4. Stock reserved: Available = {inventory.AvailableQuantity}");

		// 5. Confirm and ship order
		order.ConfirmOrder();
		order.ShipOrder();
		Console.WriteLine($"5. Order shipped: Status = {order.Status}");

		// 6. Deduct inventory after shipment
		inventory.ShipStock(3, order.Id());
		Console.WriteLine($"6. Stock shipped: On Hand = {inventory.QuantityOnHand}, Reserved = {inventory.ReservedQuantity}");

		// 7. Complete order
		order.CompleteOrder();
		Console.WriteLine($"7. Order completed: Status = {order.Status}");

		// Summary
		Console.WriteLine($"\nEvent counts - Customer: {customer.GetUnsavedEvents().Count()}, " +
			$"Order: {order.GetUnsavedEvents().Count()}, " +
			$"Inventory: {inventory.GetUnsavedEvents().Count()}");

		Console.WriteLine("\n=== Sample Complete ===");
	}
}
