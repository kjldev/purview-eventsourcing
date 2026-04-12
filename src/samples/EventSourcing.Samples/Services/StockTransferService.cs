using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

/// <summary>
/// Demonstrates multi-aggregate coordination using the saga compensation pattern.
/// Transfers stock of the same product between two locations (inventory aggregates),
/// with automatic rollback if the destination save fails.
/// </summary>
public sealed class StockTransferService(
	IQueryableEventStore<InventoryAggregate> inventoryStore
) : IStockTransferService
{
	public async Task<StockTransferResult> TransferAsync(
		string sourceLocationId,
		string destinationLocationId,
		int quantity,
		string reason,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceLocationId);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationLocationId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		if (string.Equals(sourceLocationId, destinationLocationId, StringComparison.OrdinalIgnoreCase))
			return StockTransferResult.Fail("Source and destination locations must be different.");

		var source = await inventoryStore.GetAsync(sourceLocationId, null, cancellationToken);
		if (source is null || source.Details.IsDeleted)
			return StockTransferResult.Fail("Source location not found.");

		var destination = await inventoryStore.GetAsync(destinationLocationId, null, cancellationToken);
		if (destination is null || destination.Details.IsDeleted)
			return StockTransferResult.Fail("Destination location not found.");

		if (!string.Equals(source.ProductId, destination.ProductId, StringComparison.OrdinalIgnoreCase))
			return StockTransferResult.Fail(
				$"Cannot transfer between locations holding different products " +
				$"('{source.ProductId}' → '{destination.ProductId}'). " +
				"Both locations must stock the same product."
			);

		if (source.AvailableQuantity < quantity)
			return StockTransferResult.Fail(
				$"Insufficient available stock at '{source.LocationName}'. " +
				$"Available: {source.AvailableQuantity}, requested: {quantity}."
			);

		// Step 1 — reduce source stock.
		var previousSourceQty = source.QuantityOnHand;
		source.AdjustStock(source.QuantityOnHand - quantity, $"Transfer to {destination.LocationName}: {reason}");

		var sourceSave = await inventoryStore.SaveAsync(source, null, cancellationToken);
		if (!sourceSave)
			return StockTransferResult.Fail($"Failed to save source location '{source.LocationName}'.");

		var savedSource = sourceSave.Aggregate;

		// Step 2 — add to destination; compensate if save fails.
		destination.ReceiveStock(quantity);
		var destinationSave = await inventoryStore.SaveAsync(destination, null, cancellationToken);
		if (!destinationSave)
		{
			// Compensate: restore source stock before returning failure.
			savedSource.AdjustStock(previousSourceQty, $"Compensation rollback: failed transfer to {destination.LocationName}");
			await inventoryStore.SaveAsync(savedSource, null, cancellationToken);

			return StockTransferResult.Fail(
				$"Failed to save destination location '{destination.LocationName}'. " +
				"Source stock has been restored."
			);
		}

		return StockTransferResult.Success(savedSource, destinationSave.Aggregate, quantity);
	}
}
