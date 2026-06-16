using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Demonstrates inventory management with stock tracking at a specific location.
/// Shows: validation guards, computed state, concurrency-safe operations.
/// </summary>
[GenerateAggregate]
public sealed partial class InventoryAggregate : AggregateBase
{
	public string ProductId { get; private set; } = default!;

	public string ProductName { get; private set; } = default!;

	public string LocationId { get; private set; } = default!;

	public string LocationName { get; private set; } = default!;

	[Range(0, int.MaxValue)]
	public int QuantityOnHand { get; private set; }

	[Range(0, int.MaxValue)]
	public int ReservedQuantity { get; private set; }

	// This is a read-only properety that calculates available stock based on current state.
	// It has a private setter to satisfy queries in Entity Framework stores, such as SQL server.
	public int AvailableQuantity
	{
		get => QuantityOnHand - ReservedQuantity;
		private set { }
	}

	public InventoryAggregate ReceiveStock(int quantity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		return ReceiveStock(quantityOnHand: QuantityOnHand + quantity, reservedQuantity: ReservedQuantity);
	}

	public InventoryAggregate ReserveStock(int quantity, string? orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		return quantity > AvailableQuantity
			? throw new InvalidOperationException(
				$"Cannot reserve {quantity} units. Only {AvailableQuantity} available."
			)
			: ReserveStock(quantityOnHand: QuantityOnHand, reservedQuantity: ReservedQuantity + quantity, orderId);
	}

	public InventoryAggregate ReleaseReservation(int quantity, string? orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		return quantity > ReservedQuantity
			? throw new InvalidOperationException($"Cannot release {quantity} units. Only {ReservedQuantity} reserved.")
			: ReleaseStockReservation(
				quantityOnHand: QuantityOnHand,
				reservedQuantity: ReservedQuantity - quantity,
				orderId
			);
	}

	public InventoryAggregate ShipStock(int quantity, string? orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		return quantity > ReservedQuantity
			? throw new InvalidOperationException($"Cannot ship {quantity} units. Only {ReservedQuantity} reserved.")
			: ShipStock(
				quantityOnHand: QuantityOnHand - quantity,
				reservedQuantity: ReservedQuantity - quantity,
				orderId
			);
	}

	public InventoryAggregate AdjustStock(int newQuantity, string reason)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(newQuantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		return AdjustStock(quantityOnHand: newQuantity, reservedQuantity: Math.Min(ReservedQuantity, newQuantity));
	}

	/// <summary>
	/// Updates one or more inventory metadata fields in a single operation, raising a granular
	/// event for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged.
	/// </summary>
	public InventoryAggregate UpdateDetails(string? productName = null, string? locationName = null)
	{
		if (productName is not null)
			UpdateProductName(productName);

		if (locationName is not null)
			UpdateLocationName(locationName);

		return this;
	}

	[GenerateAggregateEvent]
	public partial InventoryAggregate Create(
		string productId,
		string productName,
		string locationId,
		string locationName,
		[AggregateProperty(nameof(QuantityOnHand))] int initialQuantity = 0,
		int reservedQuantity = 0
	);

	[GenerateAggregateEvent]
	public partial InventoryAggregate ReceiveStock(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate ReserveStock(
		int quantityOnHand,
		int reservedQuantity,
		[Metadata] string? orderId
	);

	[GenerateAggregateEvent]
	public partial InventoryAggregate ReleaseStockReservation(
		int quantityOnHand,
		int reservedQuantity,
		[Metadata] string? orderId
	);

	[GenerateAggregateEvent]
	public partial InventoryAggregate ShipStock(int quantityOnHand, int reservedQuantity, [Metadata] string? orderId);

	[GenerateAggregateEvent]
	public partial InventoryAggregate AdjustStock(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate UpdateProductName(string productName);

	[GenerateAggregateEvent]
	public partial InventoryAggregate UpdateLocationName(string locationName);
}
