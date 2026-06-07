using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class OrderFulfillmentService(
    IEventStoreTransactionFactory transactionFactory,
    IQueryableEventStore store
) : IOrderFulfillmentService
{
    // Thread-safe price cache shared across all instances.
    static readonly ConcurrentDictionary<string, decimal> UnitPrices = new(
        StringComparer.OrdinalIgnoreCase
    );

    public async Task<FulfilmentResult> PlaceOrderAsync(
        string customerId,
        string inventoryId,
        int quantity,
        string? shippingAddress,
        CancellationToken cancellationToken = default
    )
    {
        var customer = await store.GetAsync<CustomerAggregate>(customerId, cancellationToken);
        if (customer is null || customer.Details.IsDeleted)
            return FulfilmentResult.Fail("Customer not found.");
        if (!customer.IsActive)
            return FulfilmentResult.Fail($"Customer '{customer.Name}' is not active.");

        var inventory = await store.GetAsync<InventoryAggregate>(inventoryId, cancellationToken);
        if (inventory is null || inventory.Details.IsDeleted)
            return FulfilmentResult.Fail("Inventory item not found.");
        if (inventory.AvailableQuantity < quantity)
            return FulfilmentResult.Fail(
                $"Insufficient stock for '{inventory.ProductName}'. Available: {inventory.AvailableQuantity}, requested: {quantity}."
            );

        var unitPrice = GetUnitPrice(inventory.ProductId);

        // Create and confirm the order.
        var order = await store.CreateAsync<OrderAggregate>(cancellationToken: cancellationToken);
        order.CreateOrder(customerId);
        order.AddLineItem(inventory.ProductId, inventory.ProductName, quantity, unitPrice);
        if (!string.IsNullOrWhiteSpace(shippingAddress))
            order.SetShippingAddress(shippingAddress);
        order.ConfirmOrder();

        inventory.ReserveStock(quantity, order.Id());

        await using var transaction = transactionFactory.Create();
        transaction.Enlist(order, store);
        transaction.Enlist(inventory, store);

        var transactionResult = await transaction.CommitAsync(cancellationToken);
        return transactionResult.Success
            ? FulfilmentResult.Success(order, inventory)
            : FulfilmentResult.Fail("Failed to place order. Nothing was saved.");
    }

    static decimal GetUnitPrice(string productId)
    {
        if (UnitPrices.TryGetValue(productId, out var cached))
            return cached;

        // Derive a stable price from the product ID using a deterministic hash for demo purposes.
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(productId));
        var hash = (int)(BitConverter.ToUInt32(hashBytes, 0) & 0x7FFFFFFF);
        var price = Math.Round(9.99m + (hash % 9000) / 100m, 2);
        return UnitPrices.GetOrAdd(productId, price);
    }
}
