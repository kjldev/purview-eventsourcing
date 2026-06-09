using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Complex aggregate demonstrating advanced patterns:
/// - Multi-property events with nested collections
/// - State machine transitions (order status)
/// - Computed properties (TotalAmount)
/// - Validation with DataAnnotations
/// - Collection management (line items)
/// </summary>
[GenerateAggregate]
public sealed partial class OrderAggregate : AggregateBase
{
	public string CustomerId { get; private set; } = default!;

	public OrderStatus Status { get; private set; }

	[Range(0, double.MaxValue)]
	public decimal TotalAmount { get; private set; }

	public ImmutableArray<OrderLineItem> LineItems { get; private set; } = [];

	public string? ShippingAddress { get; private set; }

	public string? Notes { get; private set; }

	public DateTimeOffset? ShippedAt { get; private set; }

	public DateTimeOffset? CompletedAt { get; private set; }

	// Commands
	public OrderAggregate CreateOrder(string customerId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

		return OrderCreated(customerId, status: OrderStatus.Draft);
	}

	public OrderAggregate AddLineItem(string productId, string productName, int quantity, decimal unitPrice)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only add items to draft orders.");

		var existingLineItem = LineItems.FirstOrDefault(lineItem => lineItem.ProductId == productId);
		var updatedLineItems = existingLineItem is null
			? LineItems.Add(new OrderLineItem(productId, productName, quantity, unitPrice))
			:
			[
				.. LineItems.Select(lineItem =>
					lineItem.ProductId == productId
						? lineItem with
						{
							Quantity = lineItem.Quantity + quantity,
						}
						: lineItem
				),
			];

		return OrderLineItemAdded(updatedLineItems, totalAmount: CalculateTotalAmount(updatedLineItems));
	}

	public OrderAggregate RemoveLineItem(string productId)
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only remove items from draft orders.");

		if (!LineItems.Any(li => li.ProductId == productId))
			throw new InvalidOperationException($"Product '{productId}' not found in order.");

		var updatedLineItems = LineItems.Where(lineItem => lineItem.ProductId != productId).ToImmutableArray();

		return OrderLineItemRemoved(updatedLineItems, totalAmount: CalculateTotalAmount(updatedLineItems));
	}

	public OrderAggregate SetShippingAddress(string address)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);

		return OrderShippingAddressSet(shippingAddress: address);
	}

	public OrderAggregate UpdateNotes(string? notes) => OrderNotesUpdated(notes);

	/// <summary>
	/// Updates one or more order details in a single operation, raising a granular event
	/// for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged. To clear notes, use <see cref="UpdateNotes"/> directly.
	/// </summary>
	public OrderAggregate UpdateDetails(string? shippingAddress = null, string? notes = null)
	{
		if (shippingAddress is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(shippingAddress);
			if (shippingAddress != ShippingAddress)
				OrderShippingAddressSet(shippingAddress);
		}

		if (notes is not null && notes != Notes)
			OrderNotesUpdated(notes);

		return this;
	}

	public OrderAggregate ConfirmOrder()
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only confirm draft orders.");

		if (LineItems.Length == 0)
			throw new InvalidOperationException("Cannot confirm an order with no items.");

		return OrderConfirmed(status: OrderStatus.Confirmed);
	}

	public OrderAggregate ShipOrder()
	{
		if (Status != OrderStatus.Confirmed)
			throw new InvalidOperationException("Can only ship confirmed orders.");

		if (string.IsNullOrWhiteSpace(ShippingAddress))
			throw new InvalidOperationException("Shipping address must be set before shipping.");

		return OrderShipped(status: OrderStatus.Shipped, shippedAt: DateTimeOffset.UtcNow);
	}

	public OrderAggregate CompleteOrder()
	{
		if (Status != OrderStatus.Shipped)
			throw new InvalidOperationException("Can only complete shipped orders.");

		return OrderCompleted(status: OrderStatus.Completed, completedAt: DateTimeOffset.UtcNow);
	}

	public OrderAggregate CancelOrder()
	{
		if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
			throw new InvalidOperationException("Cannot cancel a completed or already cancelled order.");

		return OrderCancelled(status: OrderStatus.Cancelled);
	}

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderCreated(string customerId, OrderStatus status);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderLineItemAdded(ImmutableArray<OrderLineItem> lineItems, decimal totalAmount);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderLineItemRemoved(ImmutableArray<OrderLineItem> lineItems, decimal totalAmount);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderShippingAddressSet(string shippingAddress);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderNotesUpdated(string? notes);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderConfirmed(OrderStatus status);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderShipped(OrderStatus status, DateTimeOffset shippedAt);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderCompleted(OrderStatus status, DateTimeOffset completedAt);

	[GenerateAggregateEvent]
	public partial OrderAggregate OrderCancelled(OrderStatus status);

	static decimal CalculateTotalAmount(ImmutableArray<OrderLineItem> lineItems) =>
		lineItems.Sum(lineItem => lineItem.Quantity * lineItem.UnitPrice);
}

public enum OrderStatus
{
	Draft,
	Confirmed,
	Shipped,
	Completed,
	Cancelled,
}

public sealed record OrderLineItem(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
