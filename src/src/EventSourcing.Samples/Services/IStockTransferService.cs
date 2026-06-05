namespace Purview.EventSourcing.Samples.Services;

public interface IStockTransferService
{
	/// <summary>
	/// Transfers stock from a source inventory record to a physical destination location using a
	/// saga compensation pattern. The destination inventory record is created automatically when the
	/// product is moved into a location for the first time. If the destination save fails, the
	/// source stock is compensated before the failure is returned.
	/// </summary>
	Task<StockTransferResult> TransferAsync(
		string sourceInventoryId,
		string destinationLocationId,
		int quantity,
		string reason,
		CancellationToken cancellationToken = default
	);
}
