using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Samples.Domain.Events;

public sealed class OrderCreatedEvent : EventBase
{
	public string CustomerId { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(CustomerId);
}

public sealed class OrderLineItemAddedEvent : EventBase
{
	public string ProductId { get; set; } = default!;
	public string ProductName { get; set; } = default!;
	public int Quantity { get; set; }
	public decimal UnitPrice { get; set; }

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(ProductId);
		hash.Add(ProductName);
		hash.Add(Quantity);
		hash.Add(UnitPrice);
	}
}

public sealed class OrderLineItemRemovedEvent : EventBase
{
	public string ProductId { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(ProductId);
}

public sealed class OrderShippingAddressSetEvent : EventBase
{
	public string Address { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(Address);
}

public sealed class OrderNotesUpdatedEvent : EventBase
{
	public string? Notes { get; set; }

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(Notes);
}

public sealed class OrderConfirmedEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}

public sealed class OrderShippedEvent : EventBase
{
	public DateTimeOffset ShippedAt { get; set; }

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(ShippedAt);
}

public sealed class OrderCompletedEvent : EventBase
{
	public DateTimeOffset CompletedAt { get; set; }

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(CompletedAt);
}

public sealed class OrderCancelledEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
