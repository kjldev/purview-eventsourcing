using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

/// <summary>
/// Demonstrates multi-aggregate coordination using a single event-store transaction.
/// Transfers stock from a source inventory aggregate to a physical destination location,
/// creating the destination inventory aggregate on demand and enlisting both aggregates in the
/// same transaction so the commit succeeds or fails atomically.
/// </summary>
public sealed class StockTransferService(
    IEventStoreTransactionFactory transactionFactory,
    IQueryableEventStore store
) : IStockTransferService
{
    public async Task<StockTransferResult> TransferAsync(
        string sourceInventoryId,
        string destinationLocationId,
        int quantity,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInventoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationLocationId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var source = await store.GetAsync<InventoryAggregate>(sourceInventoryId, cancellationToken);
        if (source is null || source.Details.IsDeleted)
            return StockTransferResult.Fail("Source stock item not found.");

        if (
            string.Equals(
                source.LocationId,
                destinationLocationId,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return StockTransferResult.Fail("Source and destination locations must be different.");

        var sourceLocation = await store.GetAsync<LocationAggregate>(
            source.LocationId,
            cancellationToken
        );
        if (sourceLocation is null || sourceLocation.Details.IsDeleted)
            return StockTransferResult.Fail($"Source location '{source.LocationId}' not found.");

        var destinationLocation = await store.GetAsync<LocationAggregate>(
            destinationLocationId,
            cancellationToken
        );
        if (destinationLocation is null || destinationLocation.Details.IsDeleted)
            return StockTransferResult.Fail("Destination location not found.");

        if (source.AvailableQuantity < quantity)
            return StockTransferResult.Fail(
                $"Insufficient available stock at '{sourceLocation.LocationName}'. "
                    + $"Available: {source.AvailableQuantity}, requested: {quantity}."
            );

        var destination = await store.FirstOrDefaultAsync<InventoryAggregate>(
            i => i.ProductId == source.ProductId && i.LocationId == destinationLocation.LocationId,
            cancellationToken
        );
        if (destination is null)
        {
            destination = await store.CreateAsync<InventoryAggregate>(
                cancellationToken: cancellationToken
            );
            destination.Initialize(
                source.ProductId,
                source.ProductName,
                destinationLocation.LocationId,
                destinationLocation.LocationName,
                initialQuantity: 0
            );
        }

        source.AdjustStock(
            source.QuantityOnHand - quantity,
            $"Transfer to {destinationLocation.LocationName}: {reason}"
        );

        destination.ReceiveStock(quantity);

        await using var transaction = transactionFactory.Create();
        transaction.Enlist(source, store);
        transaction.Enlist(destination, store);

        var transactionResult = await transaction.CommitAsync(cancellationToken);
        return transactionResult.Success
            ? StockTransferResult.Success(source, destination, quantity)
            : StockTransferResult.Fail("Failed to transfer stock. Nothing was saved.");
    }
}
