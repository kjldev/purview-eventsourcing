using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

/// <summary>
/// Demonstrates multi-aggregate coordination using the saga compensation pattern.
/// Transfers stock from a source inventory aggregate to a physical destination location,
/// creating the destination inventory aggregate on demand and compensating the source if the
/// destination save fails.
/// </summary>
public sealed class StockTransferService(IQueryableEventStore store) : IStockTransferService
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

		var source = await store.GetAsync<InventoryAggregate>(sourceInventoryId, null, cancellationToken);
		if (source is null || source.Details.IsDeleted)
			return StockTransferResult.Fail("Source stock item not found.");

		if (string.Equals(source.LocationId, destinationLocationId, StringComparison.OrdinalIgnoreCase))
			return StockTransferResult.Fail("Source and destination locations must be different.");

		var sourceLocation = await store.GetAsync<LocationAggregate>(source.LocationId, null, cancellationToken);
		if (sourceLocation is null || sourceLocation.Details.IsDeleted)
			return StockTransferResult.Fail($"Source location '{source.LocationId}' not found.");

		var destinationLocation = await store.GetAsync<LocationAggregate>(
			destinationLocationId,
			null,
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
			destination = await store.CreateAsync<InventoryAggregate>(cancellationToken: cancellationToken);
			destination.Initialize(
				source.ProductId,
				source.ProductName,
				destinationLocation.LocationId,
				destinationLocation.LocationName,
				initialQuantity: 0
			);
		}

		// Step 1 — reduce source stock.
		var previousSourceQty = source.QuantityOnHand;
		source.AdjustStock(
			source.QuantityOnHand - quantity,
			$"Transfer to {destinationLocation.LocationName}: {reason}"
		);

		var sourceSave = await store.SaveAsync(source, null, cancellationToken);
		if (!sourceSave)
			return StockTransferResult.Fail($"Failed to save source location '{sourceLocation.LocationName}'.");

		var savedSource = sourceSave.Aggregate;

		// Step 2 — add to destination; compensate if save fails.
		destination.ReceiveStock(quantity);
		var destinationSave = await store.SaveAsync(destination, null, cancellationToken);
		if (!destinationSave)
		{
			// Compensate: restore source stock before returning failure.
			savedSource.AdjustStock(
				previousSourceQty,
				$"Compensation rollback: failed transfer to {destinationLocation.LocationName}"
			);

			var compensationSave = await store.SaveAsync(savedSource, null, cancellationToken);
			return compensationSave
				? StockTransferResult.Fail(
				$"Failed to save destination location '{destinationLocation.LocationName}'. "
					+ "Source stock has been restored."
			)
				: StockTransferResult.Fail(
					$"Failed to save destination location '{destinationLocation.LocationName}'. "
						+ $"Compensation to restore source location '{sourceLocation.LocationName}' also failed."
				);
		}

		return StockTransferResult.Success(savedSource, destinationSave.Aggregate, quantity);
	}
}
