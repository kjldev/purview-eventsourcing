namespace Purview.EventSourcing.Samples.Domain;

partial class InventoryAggregate
{
	partial void OnCreatingInventoryCreated(
		ref string productId,
		ref string productName,
		ref string locationId,
		ref string locationName,
		ref int quantityOnHand,
		ref int reservedQuantity
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
		ArgumentOutOfRangeException.ThrowIfNegative(quantityOnHand);
	}
}
