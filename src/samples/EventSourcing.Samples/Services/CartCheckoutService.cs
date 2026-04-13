using System.Collections.Concurrent;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class CartCheckoutService(
	IEventStoreTransactionFactory transactionFactory,
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

		var inventoryReservations = new Dictionary<string, InventoryReservation>(StringComparer.OrdinalIgnoreCase);
		foreach (var item in items)
		{
			if (!inventoryReservations.TryGetValue(item.InventoryId, out var reservation))
			{
				var inventory = await inventoryStore.GetAsync(item.InventoryId, null, cancellationToken);
				if (inventory is null || inventory.Details.IsDeleted)
					return CartCheckoutResult.Fail($"Product '{item.ProductName}' is no longer available.");

				reservation = new InventoryReservation(inventory);
				inventoryReservations.Add(item.InventoryId, reservation);
			}

			reservation.Add(item.Quantity);
		}

		foreach (var reservation in inventoryReservations.Values)
		{
			if (reservation.Aggregate.AvailableQuantity < reservation.Quantity)
			{
				return CartCheckoutResult.Fail(
					$"Insufficient stock for '{reservation.Aggregate.ProductName}'. Available: {reservation.Aggregate.AvailableQuantity}, requested: {reservation.Quantity}.");
			}
		}

		var order = await orderStore.CreateAsync(null, cancellationToken);
		order.CreateOrder(customerId);

		foreach (var item in items)
		{
			var inventory = inventoryReservations[item.InventoryId].Aggregate;
			var unitPrice = GetUnitPrice(inventory.ProductId);
			order.AddLineItem(inventory.ProductId, inventory.ProductName, item.Quantity, unitPrice);
		}

		if (!string.IsNullOrWhiteSpace(shippingAddress))
			order.SetShippingAddress(shippingAddress);

		order.ConfirmOrder();

		foreach (var reservation in inventoryReservations.Values)
			reservation.Aggregate.ReserveStock(reservation.Quantity, order.Id());

		await using var transaction = transactionFactory.Create();
		transaction.Enlist(order, orderStore);

		foreach (var reservation in inventoryReservations.Values)
			transaction.Enlist(reservation.Aggregate, inventoryStore);

		var transactionResult = await transaction.CommitAsync(cancellationToken);
		if (!transactionResult.Success)
			return CartCheckoutResult.Fail("Unable to complete checkout. Nothing was saved. Please review your cart and try again.");

		return CartCheckoutResult.Success(order);
	}

	static decimal GetUnitPrice(string productId)
	{
		if (_unitPrices.TryGetValue(productId, out var cached))
			return cached;

		var hash = Math.Abs(productId.GetHashCode());
		var price = Math.Round(9.99m + (hash % 9000) / 100m, 2);
		return _unitPrices.GetOrAdd(productId, price);
	}

	sealed class InventoryReservation(InventoryAggregate aggregate)
	{
		public InventoryAggregate Aggregate { get; } = aggregate;
		public int Quantity { get; private set; }

		public void Add(int quantity) => Quantity += quantity;
	}
}
