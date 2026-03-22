using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Demonstrates inventory management with stock tracking.
/// Shows: validation guards, computed state, concurrency-safe operations.
/// </summary>
public sealed class InventoryAggregate : AggregateBase
{
	public string ProductId { get; private set; } = default!;
	public string ProductName { get; private set; } = default!;

	[Range(0, int.MaxValue)]
	public int QuantityOnHand { get; private set; }

	[Range(0, int.MaxValue)]
	public int ReservedQuantity { get; private set; }

	public int AvailableQuantity => QuantityOnHand - ReservedQuantity;

	protected override void RegisterEvents()
	{
		Register<InventoryInitializedEvent>(Apply);
		Register<StockReceivedEvent>(Apply);
		Register<StockReservedEvent>(Apply);
		Register<StockReservationReleasedEvent>(Apply);
		Register<StockShippedEvent>(Apply);
		Register<StockAdjustedEvent>(Apply);
	}

	// Commands
	public void Initialize(string productId, string productName, int initialQuantity = 0)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentException.ThrowIfNullOrWhiteSpace(productName);
		ArgumentOutOfRangeException.ThrowIfNegative(initialQuantity);

		RecordAndApply(new InventoryInitializedEvent
		{
			ProductId = productId,
			ProductName = productName,
			InitialQuantity = initialQuantity
		});
	}

	public void ReceiveStock(int quantity)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		RecordAndApply(new StockReceivedEvent { Quantity = quantity });
	}

	public void ReserveStock(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		if (quantity > AvailableQuantity)
			throw new InvalidOperationException(
				$"Cannot reserve {quantity} units. Only {AvailableQuantity} available.");

		RecordAndApply(new StockReservedEvent { Quantity = quantity, OrderId = orderId });
	}

	public void ReleaseReservation(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		if (quantity > ReservedQuantity)
			throw new InvalidOperationException(
				$"Cannot release {quantity} units. Only {ReservedQuantity} reserved.");

		RecordAndApply(new StockReservationReleasedEvent { Quantity = quantity, OrderId = orderId });
	}

	public void ShipStock(int quantity, string orderId)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

		if (quantity > ReservedQuantity)
			throw new InvalidOperationException(
				$"Cannot ship {quantity} units. Only {ReservedQuantity} reserved.");

		RecordAndApply(new StockShippedEvent { Quantity = quantity, OrderId = orderId });
	}

	public void AdjustStock(int newQuantity, string reason)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(newQuantity);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		RecordAndApply(new StockAdjustedEvent { NewQuantity = newQuantity, Reason = reason });
	}

	// Apply methods
	void Apply(InventoryInitializedEvent @event)
	{
		ProductId = @event.ProductId;
		ProductName = @event.ProductName;
		QuantityOnHand = @event.InitialQuantity;
	}

	void Apply(StockReceivedEvent @event) => QuantityOnHand += @event.Quantity;

	void Apply(StockReservedEvent @event) => ReservedQuantity += @event.Quantity;

	void Apply(StockReservationReleasedEvent @event) => ReservedQuantity -= @event.Quantity;

	void Apply(StockShippedEvent @event)
	{
		QuantityOnHand -= @event.Quantity;
		ReservedQuantity -= @event.Quantity;
	}

	void Apply(StockAdjustedEvent @event)
	{
		QuantityOnHand = @event.NewQuantity;
		if (ReservedQuantity > QuantityOnHand)
			ReservedQuantity = QuantityOnHand;
	}
}
