using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Complex aggregate demonstrating advanced patterns:
/// - Multi-property events with nested collections
/// - State machine transitions (order status)
/// - Computed properties (TotalAmount)
/// - Validation with DataAnnotations
/// - Collection management (line items)
/// </summary>
public sealed class OrderAggregate : AggregateBase
{
	public string CustomerId { get; private set; } = default!;
	public OrderStatus Status { get; private set; }

	[Range(0, double.MaxValue)]
	public decimal TotalAmount { get; private set; }

	public List<OrderLineItem> LineItems { get; } = [];
	public string? ShippingAddress { get; private set; }
	public string? Notes { get; private set; }
	public DateTimeOffset? ShippedAt { get; private set; }
	public DateTimeOffset? CompletedAt { get; private set; }

	protected override void RegisterEvents()
	{
		Register<OrderCreatedEvent>(Apply);
		Register<OrderLineItemAddedEvent>(Apply);
		Register<OrderLineItemRemovedEvent>(Apply);
		Register<OrderShippingAddressSetEvent>(Apply);
		Register<OrderNotesUpdatedEvent>(Apply);
		Register<OrderConfirmedEvent>(Apply);
		Register<OrderShippedEvent>(Apply);
		Register<OrderCompletedEvent>(Apply);
		Register<OrderCancelledEvent>(Apply);
	}

	// Commands
	public void CreateOrder(string customerId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
		RecordAndApply(new OrderCreatedEvent { CustomerId = customerId });
	}

	public void AddLineItem(string productId, string productName, int quantity, decimal unitPrice)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
		ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only add items to draft orders.");

		RecordAndApply(new OrderLineItemAddedEvent
		{
			ProductId = productId,
			ProductName = productName,
			Quantity = quantity,
			UnitPrice = unitPrice
		});
	}

	public void RemoveLineItem(string productId)
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only remove items from draft orders.");

		if (!LineItems.Any(li => li.ProductId == productId))
			throw new InvalidOperationException($"Product '{productId}' not found in order.");

		RecordAndApply(new OrderLineItemRemovedEvent { ProductId = productId });
	}

	public void SetShippingAddress(string address)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		RecordAndApply(new OrderShippingAddressSetEvent { Address = address });
	}

	public void UpdateNotes(string? notes)
	{
		RecordAndApply(new OrderNotesUpdatedEvent { Notes = notes });
	}

	public void ConfirmOrder()
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only confirm draft orders.");

		if (LineItems.Count == 0)
			throw new InvalidOperationException("Cannot confirm an order with no items.");

		RecordAndApply(new OrderConfirmedEvent());
	}

	public void ShipOrder()
	{
		if (Status != OrderStatus.Confirmed)
			throw new InvalidOperationException("Can only ship confirmed orders.");

		if (string.IsNullOrWhiteSpace(ShippingAddress))
			throw new InvalidOperationException("Shipping address must be set before shipping.");

		RecordAndApply(new OrderShippedEvent { ShippedAt = DateTimeOffset.UtcNow });
	}

	public void CompleteOrder()
	{
		if (Status != OrderStatus.Shipped)
			throw new InvalidOperationException("Can only complete shipped orders.");

		RecordAndApply(new OrderCompletedEvent { CompletedAt = DateTimeOffset.UtcNow });
	}

	public void CancelOrder()
	{
		if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
			throw new InvalidOperationException("Cannot cancel a completed or already cancelled order.");

		RecordAndApply(new OrderCancelledEvent());
	}

	// Apply methods
	void Apply(OrderCreatedEvent @event)
	{
		CustomerId = @event.CustomerId;
		Status = OrderStatus.Draft;
	}

	void Apply(OrderLineItemAddedEvent @event)
	{
		var existing = LineItems.FirstOrDefault(li => li.ProductId == @event.ProductId);
		if (existing is not null)
		{
			existing.Quantity += @event.Quantity;
		}
		else
		{
			LineItems.Add(new OrderLineItem
			{
				ProductId = @event.ProductId,
				ProductName = @event.ProductName,
				Quantity = @event.Quantity,
				UnitPrice = @event.UnitPrice
			});
		}

		RecalculateTotal();
	}

	void Apply(OrderLineItemRemovedEvent @event)
	{
		LineItems.RemoveAll(li => li.ProductId == @event.ProductId);
		RecalculateTotal();
	}

	void Apply(OrderShippingAddressSetEvent @event) => ShippingAddress = @event.Address;
	void Apply(OrderNotesUpdatedEvent @event) => Notes = @event.Notes;
	void Apply(OrderConfirmedEvent _) => Status = OrderStatus.Confirmed;

	void Apply(OrderShippedEvent @event)
	{
		Status = OrderStatus.Shipped;
		ShippedAt = @event.ShippedAt;
	}

	void Apply(OrderCompletedEvent @event)
	{
		Status = OrderStatus.Completed;
		CompletedAt = @event.CompletedAt;
	}

	void Apply(OrderCancelledEvent _) => Status = OrderStatus.Cancelled;

	void RecalculateTotal()
	{
		TotalAmount = LineItems.Sum(li => li.Quantity * li.UnitPrice);
	}
}

public enum OrderStatus
{
	Draft,
	Confirmed,
	Shipped,
	Completed,
	Cancelled
}

public class OrderLineItem
{
	public string ProductId { get; set; } = default!;
	public string ProductName { get; set; } = default!;
	public int Quantity { get; set; }
	public decimal UnitPrice { get; set; }
}
