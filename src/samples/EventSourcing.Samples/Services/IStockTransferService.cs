namespace Purview.EventSourcing.Samples.Services;

public interface IStockTransferService
{
	/// <summary>
	/// Transfers stock of the same product between two locations using a saga compensation pattern.
	/// Both <paramref name="sourceLocationId"/> and <paramref name="destinationLocationId"/> must
	/// be inventory aggregates holding the same product. If the destination save fails, the source
	/// stock is automatically restored.
	/// </summary>
	Task<StockTransferResult> TransferAsync(
		string sourceLocationId,
		string destinationLocationId,
		int quantity,
		string reason,
		CancellationToken cancellationToken = default
	);
}
