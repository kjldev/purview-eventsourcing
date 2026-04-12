using System.Collections.Concurrent;
using Purview.EventSourcing;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class CartCheckoutService(
	IQueryableEventStore<OrderAggregate> orderStore,
	IQueryableEventStore<InventoryAggregate> inventoryStore,
	IQueryableEventStore<CustomerAggregate> customerStore
) : ICartCheckoutService
{
	static readonly ConcurrentDictionary<string, decimal> _unitPrices = new(StringComparer.OrdinalIgnoreCase);

	public async Task<CartCheckoutResult> CheckoutAsync(
		string customerId,
		IReadOnlyList<CartItem> items,
		string? shippingAddress,
		CancellationToken cancellationToken = default
	)
	{
		if (items.Count == 0)
			return CartCheckoutResult.Fail("Cart is empty.");

		var customer = await customerStore.GetAsync(customerId, null, cancellationToken);
		if (customer is null || customer.Details.IsDeleted)
			return CartCheckoutResult.Fail("Customer not found.");
		if (!customer.IsActive)
			return CartCheckoutResult.Fail($"Customer '{customer.Name}' is not active.");

		var inventoryItems = new List<InventoryAggregate>();
		foreach (var item in items)
		{
			var inv = await inventoryStore.GetAsync(item.InventoryId, null, cancellationToken);
			if (inv is null || inv.Details.IsDeleted)
				return CartCheckoutResult.Fail($"Product '{item.ProductName}' is no longer available.");
			if (inv.AvailableQuantity < item.Quantity)
				return CartCheckoutResult.Fail(
					$"Insufficient stock for '{inv.ProductName}'. Available: {inv.AvailableQuantity}, requested: {item.Quantity}.");
			inventoryItems.Add(inv);
		}

		var order = await orderStore.CreateAsync(null, cancellationToken);
		order.CreateOrder(customerId);

		foreach (var (cartItem, invItem) in items.Zip(inventoryItems))
		{
			var unitPrice = GetUnitPrice(invItem.ProductId);
			order.AddLineItem(invItem.ProductId, invItem.ProductName, cartItem.Quantity, unitPrice);
		}

		if (!string.IsNullOrWhiteSpace(shippingAddress))
			order.SetShippingAddress(shippingAddress);

		order.ConfirmOrder();

		var orderSave = await orderStore.SaveAsync(order, null, cancellationToken);
		if (!orderSave)
			return CartCheckoutResult.Fail("Failed to save order.");

		var savedOrder = orderSave.Aggregate;

		foreach (var (cartItem, invItem) in items.Zip(inventoryItems))
		{
			invItem.ReserveStock(cartItem.Quantity, savedOrder.Id());
			var invSave = await inventoryStore.SaveAsync(invItem, null, cancellationToken);
			if (!invSave)
			{
				savedOrder.CancelOrder();
				await orderStore.SaveAsync(savedOrder, null, cancellationToken);
				return CartCheckoutResult.Fail($"Failed to reserve stock for '{cartItem.ProductName}'. Order has been cancelled.");
			}
		}

		return CartCheckoutResult.Success(savedOrder);
	}

	static decimal GetUnitPrice(string productId)
	{
		if (_unitPrices.TryGetValue(productId, out var cached))
			return cached;

		var hash = Math.Abs(productId.GetHashCode());
		var price = Math.Round(9.99m + (hash % 9000) / 100m, 2);
		return _unitPrices.GetOrAdd(productId, price);
	}
}
