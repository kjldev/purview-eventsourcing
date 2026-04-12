using System.Collections.Concurrent;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class OrderFulfillmentService(
	IQueryableEventStore<OrderAggregate> orderStore,
	IQueryableEventStore<InventoryAggregate> inventoryStore,
	IQueryableEventStore<CustomerAggregate> customerStore
) : IOrderFulfillmentService
{
	// Thread-safe price cache shared across all instances.
	static readonly ConcurrentDictionary<string, decimal> _unitPrices = new(StringComparer.OrdinalIgnoreCase);

	public async Task<FulfilmentResult> PlaceOrderAsync(
		string customerId,
		string inventoryId,
		int quantity,
		string? shippingAddress,
		CancellationToken cancellationToken = default
	)
	{
		var customer = await customerStore.GetAsync(customerId, null, cancellationToken);
		if (customer is null || customer.Details.IsDeleted)
			return FulfilmentResult.Fail("Customer not found.");
		if (!customer.IsActive)
			return FulfilmentResult.Fail($"Customer '{customer.Name}' is not active.");

		var inventory = await inventoryStore.GetAsync(inventoryId, null, cancellationToken);
		if (inventory is null || inventory.Details.IsDeleted)
			return FulfilmentResult.Fail("Inventory item not found.");
		if (inventory.AvailableQuantity < quantity)
			return FulfilmentResult.Fail(
				$"Insufficient stock for '{inventory.ProductName}'. Available: {inventory.AvailableQuantity}, requested: {quantity}."
			);

		var unitPrice = GetUnitPrice(inventory.ProductId);

		// Create and confirm the order.
		var order = await orderStore.CreateAsync(null, cancellationToken);
		order.CreateOrder(customerId);
		order.AddLineItem(inventory.ProductId, inventory.ProductName, quantity, unitPrice);
		if (!string.IsNullOrWhiteSpace(shippingAddress))
			order.SetShippingAddress(shippingAddress);
		order.ConfirmOrder();

		var orderSave = await orderStore.SaveAsync(order, null, cancellationToken);
		if (!orderSave)
			return FulfilmentResult.Fail("Failed to save order.");

		var savedOrder = orderSave.Aggregate;

		// Reserve stock — if this fails, compensate by cancelling the order.
		inventory.ReserveStock(quantity, savedOrder.Id());
		var inventorySave = await inventoryStore.SaveAsync(inventory, null, cancellationToken);
		if (!inventorySave)
		{
			savedOrder.CancelOrder();
			await orderStore.SaveAsync(savedOrder, null, cancellationToken);
			return FulfilmentResult.Fail("Failed to reserve inventory. Order has been cancelled.");
		}

		return FulfilmentResult.Success(savedOrder, inventorySave.Aggregate);
	}

	static decimal GetUnitPrice(string productId)
	{
		if (_unitPrices.TryGetValue(productId, out var cached))
			return cached;

		// Derive a stable price from the product ID hash for demo purposes.
		var hash = Math.Abs(productId.GetHashCode());
		var price = Math.Round(9.99m + (hash % 9000) / 100m, 2);
		return _unitPrices.GetOrAdd(productId, price);
	}
}
