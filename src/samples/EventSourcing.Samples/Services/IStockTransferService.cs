namespace Purview.EventSourcing.Samples.Services;

public interface IStockTransferService
{
	/// <summary>
	/// Transfers stock from one inventory item to another using a saga compensation pattern.
	/// If the destination save fails, the source stock is automatically restored.
	/// </summary>
	Task<StockTransferResult> TransferAsync(
		string sourceId,
		string destinationId,
		int quantity,
		string reason,
		CancellationToken cancellationToken = default
	);
}
