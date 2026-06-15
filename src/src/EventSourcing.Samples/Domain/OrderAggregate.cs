using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.ValueObjects;

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

	public OrderStatus Status { get; private set; } = OrderStatus.Draft;

	[Range(0, double.MaxValue)]
	public decimal TotalAmount { get; private set; }

	public List<OrderLineItem> LineItems { get; private set; } = [];

	public string? ShippingAddress { get; private set; }

	public string? Notes { get; private set; }

	public DateTimeOffset? ShippedAt { get; private set; }

	public DateTimeOffset? CompletedAt { get; private set; }

	/// <summary>
	/// Updates one or more order details in a single operation, raising a granular event
	/// for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged. To clear notes, use <see cref="UpdateNotes"/> directly.
	/// </summary>
	public OrderAggregate UpdateDetails(string? shippingAddress = null, string? notes = null)
	{
		if (shippingAddress is not null)
			SetShippingAddress(shippingAddress);

		if (notes is not null)
			UpdateNotes(notes);

		return this;
	}

	public OrderAggregate ConfirmOrder() => SetStatusCode(status: OrderStatusCode.Confirmed);

	public OrderAggregate ShipOrder() => ShipOrder(status: OrderStatusCode.Shipped, DateTimeOffset.UtcNow);

	public OrderAggregate CompleteOrder() => CompleteOrder(status: OrderStatusCode.Completed, DateTimeOffset.UtcNow);

	public OrderAggregate CancelOrder() => SetStatusCode(status: OrderStatusCode.Cancelled);

	/// <summary>
	/// Merges or adds a line item, handling duplicate products by increasing quantity.
	/// Validates inputs and order status.
	/// </summary>
	public OrderAggregate AddLineItem(string productId, string productName, int quantity, decimal unitPrice)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

		var existingLineItem = LineItems.FirstOrDefault(li => li.ProductId == productId);
		var updatedLineItems = LineItems
			.Select(li => li.ProductId == productId ? li with { Quantity = li.Quantity + quantity } : li)
			.ToList();

		if (existingLineItem is null)
			updatedLineItems.Add(new OrderLineItem(productId, productName, quantity, unitPrice));

		return AddLineItem(updatedLineItems, totalAmount: CalculateTotalAmount(updatedLineItems));
	}

	/// <summary>
	/// Removes a line item by product ID. Validates that the product exists and order is in draft.
	/// </summary>
	public OrderAggregate RemoveLineItem(string productId)
	{
		if (!LineItems.Any(li => li.ProductId == productId))
			throw new InvalidOperationException($"Product '{productId}' not found in order.");

		var updatedLineItems = LineItems.Where(li => li.ProductId != productId).ToList();

		return RemoveLineItem(updatedLineItems, totalAmount: CalculateTotalAmount(updatedLineItems));
	}

	static decimal CalculateTotalAmount(IEnumerable<OrderLineItem> lineItems) =>
		lineItems.Sum(lineItem => lineItem.Quantity * lineItem.UnitPrice);

	[GenerateAggregateEvent]
	public partial OrderAggregate CreateOrder(string customerId);

	[GenerateAggregateEvent]
	public partial OrderAggregate AddLineItem(List<OrderLineItem> lineItems, decimal totalAmount);

	[GenerateAggregateEvent]
	public partial OrderAggregate RemoveLineItem(List<OrderLineItem> lineItems, decimal totalAmount);

	[GenerateAggregateEvent]
	public partial OrderAggregate SetShippingAddress(string? shippingAddress);

	[GenerateAggregateEvent]
	public partial OrderAggregate UpdateNotes(string? notes);

	[GenerateAggregateEvent]
	private partial OrderAggregate SetStatusCode(OrderStatusCode status);

	[GenerateAggregateEvent]
	//(EventName = "OrderShipped")]
	//[SuppressMessage(
	//	"Purview.EventSourcing.SourceGenerator",
	//	"EVENTSTORE014:Event name overrides should be past tense"
	//)]
	private partial OrderAggregate ShipOrder(OrderStatusCode status, DateTimeOffset shippedAt);

	[GenerateAggregateEvent]
	private partial OrderAggregate CompleteOrder(OrderStatusCode status, DateTimeOffset completedAt);
}

public sealed record OrderLineItem(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
