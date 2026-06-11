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

	public int AvailableQuantity => QuantityOnHand - ReservedQuantity;

	// Commands
	public InventoryAggregate Initialize(
		string productId,
		string productName,
		string locationId,
		string locationName,
		int initialQuantity = 0
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
		ArgumentOutOfRangeException.ThrowIfNegative(initialQuantity);

		return InventoryInitialized(
			productId,
			productName,
			locationId,
			locationName,
			quantityOnHand: initialQuantity,
			reservedQuantity: 0
		);
	}

	public InventoryAggregate ReceiveStock(int quantity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

		return StockReceived(quantityOnHand: QuantityOnHand + quantity, reservedQuantity: ReservedQuantity);
	}

	public InventoryAggregate ReserveStock(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		return quantity > AvailableQuantity
			? throw new InvalidOperationException(
				$"Cannot reserve {quantity} units. Only {AvailableQuantity} available."
			)
			: StockReserved(quantityOnHand: QuantityOnHand, reservedQuantity: ReservedQuantity + quantity);
	}

	public InventoryAggregate ReleaseReservation(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		return quantity > ReservedQuantity
			? throw new InvalidOperationException($"Cannot release {quantity} units. Only {ReservedQuantity} reserved.")
			: StockReservationReleased(quantityOnHand: QuantityOnHand, reservedQuantity: ReservedQuantity - quantity);
	}

	public InventoryAggregate ShipStock(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		return quantity > ReservedQuantity
			? throw new InvalidOperationException($"Cannot ship {quantity} units. Only {ReservedQuantity} reserved.")
			: StockShipped(quantityOnHand: QuantityOnHand - quantity, reservedQuantity: ReservedQuantity - quantity);
	}

	public InventoryAggregate AdjustStock(int newQuantity, string reason)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(newQuantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		return StockAdjusted(quantityOnHand: newQuantity, reservedQuantity: Math.Min(ReservedQuantity, newQuantity));
	}

	/// <summary>
	/// Updates one or more inventory metadata fields in a single operation, raising a granular
	/// event for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged.
	/// </summary>
	public InventoryAggregate UpdateDetails(string? productName = null, string? locationName = null)
	{
		if (productName is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(productName);
			if (productName != ProductName)
				ProductNameUpdated(productName);
		}

		if (locationName is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
			if (locationName != LocationName)
				LocationNameUpdated(locationName);
		}

		return this;
	}

	[GenerateAggregateEvent]
	public partial InventoryAggregate InventoryInitialized(
		string productId,
		string productName,
		string locationId,
		string locationName,
		int quantityOnHand,
		int reservedQuantity
	);

	[GenerateAggregateEvent]
	public partial InventoryAggregate StockReceived(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate StockReserved(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate StockReservationReleased(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate StockShipped(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate StockAdjusted(int quantityOnHand, int reservedQuantity);

	[GenerateAggregateEvent]
	public partial InventoryAggregate ProductNameUpdated(string productName);

	[GenerateAggregateEvent]
	public partial InventoryAggregate LocationNameUpdated(string locationName);
}
