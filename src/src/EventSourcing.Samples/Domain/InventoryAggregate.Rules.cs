namespace Purview.EventSourcing.Samples.Domain;

partial class InventoryAggregate
{
	partial void OnRaisingCreatedEvent(
		ref string productId,
		ref string productName,
		ref string locationId,
		ref string locationName,
		ref int initialQuantity,
		ref int reservedQuantity
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
		ArgumentOutOfRangeException.ThrowIfNegative(initialQuantity);
		ArgumentOutOfRangeException.ThrowIfNegative(reservedQuantity);
	}
}
