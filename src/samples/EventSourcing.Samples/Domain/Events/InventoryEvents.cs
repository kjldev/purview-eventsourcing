using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Samples.Domain.Events;

public sealed class InventoryInitializedEvent : EventBase
{
	public string ProductId { get; set; } = default!;
	public string ProductName { get; set; } = default!;
	public int InitialQuantity { get; set; }

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(ProductId);
		hash.Add(ProductName);
		hash.Add(InitialQuantity);
	}
}

public sealed class StockReceivedEvent : EventBase
{
	public int Quantity { get; set; }

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(Quantity);
}

public sealed class StockReservedEvent : EventBase
{
	public int Quantity { get; set; }
	public string OrderId { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Quantity);
		hash.Add(OrderId);
	}
}

public sealed class StockReservationReleasedEvent : EventBase
{
	public int Quantity { get; set; }
	public string OrderId { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Quantity);
		hash.Add(OrderId);
	}
}

public sealed class StockShippedEvent : EventBase
{
	public int Quantity { get; set; }
	public string OrderId { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Quantity);
		hash.Add(OrderId);
	}
}

public sealed class StockAdjustedEvent : EventBase
{
	public int NewQuantity { get; set; }
	public string Reason { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(NewQuantity);
		hash.Add(Reason);
	}
}
