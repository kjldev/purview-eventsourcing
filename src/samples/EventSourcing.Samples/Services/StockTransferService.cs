using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

/// <summary>
/// Demonstrates multi-aggregate coordination using the saga compensation pattern.
/// Transfers stock from one inventory item to another with automatic rollback
/// if the destination save fails.
/// </summary>
public sealed class StockTransferService(
	IQueryableEventStore<InventoryAggregate> inventoryStore
) : IStockTransferService
{
	public async Task<StockTransferResult> TransferAsync(
		string sourceId,
		string destinationId,
		int quantity,
		string reason,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		if (string.Equals(sourceId, destinationId, StringComparison.OrdinalIgnoreCase))
			return StockTransferResult.Fail("Source and destination must be different inventory items.");

		var source = await inventoryStore.GetAsync(sourceId, null, cancellationToken);
		if (source is null || source.Details.IsDeleted)
			return StockTransferResult.Fail("Source inventory item not found.");

		var destination = await inventoryStore.GetAsync(destinationId, null, cancellationToken);
		if (destination is null || destination.Details.IsDeleted)
			return StockTransferResult.Fail("Destination inventory item not found.");

		if (source.AvailableQuantity < quantity)
			return StockTransferResult.Fail(
				$"Insufficient available stock in '{source.ProductName}'. " +
				$"Available: {source.AvailableQuantity}, requested: {quantity}."
			);

		// Step 1 — reduce source stock.
		var previousSourceQty = source.QuantityOnHand;
		source.AdjustStock(source.QuantityOnHand - quantity, $"Transfer to {destination.ProductName}: {reason}");

		var sourceSave = await inventoryStore.SaveAsync(source, null, cancellationToken);
		if (!sourceSave)
			return StockTransferResult.Fail($"Failed to save source inventory '{source.ProductName}'.");

		var savedSource = sourceSave.Aggregate;

		// Step 2 — add to destination; compensate if save fails.
		destination.ReceiveStock(quantity);
		var destinationSave = await inventoryStore.SaveAsync(destination, null, cancellationToken);
		if (!destinationSave)
		{
			// Compensate: restore source stock before returning failure.
			savedSource.AdjustStock(previousSourceQty, $"Compensation rollback: failed transfer to {destination.ProductName}");
			await inventoryStore.SaveAsync(savedSource, null, cancellationToken);

			return StockTransferResult.Fail(
				$"Failed to save destination inventory '{destination.ProductName}'. " +
				"Source stock has been restored."
			);
		}

		return StockTransferResult.Success(savedSource, destinationSave.Aggregate, quantity);
	}
}
