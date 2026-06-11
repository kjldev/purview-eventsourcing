using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Serialization;
using Purview.EventSourcing.ValueObjects;

namespace Purview.EventSourcing.Samples.ValueObjects;

/// <summary>
/// Value object wrapping <see cref="OrderStatusCode"/> that enforces valid state-machine transitions.
/// Strict <see cref="Create(OrderStatusCode, in ValueObjectContext{OrderAggregate})"/> validates the
/// current aggregate state; <see cref="Hydrate(OrderStatusCode)"/> is used for non-strict
/// persistence hydration and does not re-run transition rules.
/// </summary>
[Scalar(GenerateImplicitFromPrimitive = false)]
public readonly partial record struct OrderStatus : IContextualValueObject<OrderStatus, OrderStatusCode, OrderAggregate>
{
	public OrderStatusCode Value { get; }

	OrderStatus(OrderStatusCode value) => Value = value;

	static partial void OnValidate(OrderStatusCode value)
	{
		if (!Enum.IsDefined(value))
			throw new ArgumentException($"'{value}' is not a defined {nameof(OrderStatusCode)}.", nameof(value));
	}

	/// <summary>
	/// Creates an <see cref="OrderStatus"/> with full state-machine validation against the current
	/// aggregate state.  Use this path for new commands/events; use <see cref="Hydrate"/> for replay.
	/// </summary>
	public static OrderStatus Create(OrderStatusCode value, in ValueObjectContext<OrderAggregate> context)
	{
		var current = context.Aggregate.Status.Value;

		if (!IsValidTransition(current, value))
		{
			throw new InvalidOperationException($"Cannot transition order status from {current} to {value}.");
		}

		if (value == OrderStatusCode.Confirmed && context.Aggregate.LineItems.IsEmpty)
			throw new InvalidOperationException("Cannot confirm an order with no line items.");

		return value == OrderStatusCode.Shipped && string.IsNullOrWhiteSpace(context.Aggregate.ShippingAddress)
			? throw new InvalidOperationException("Cannot ship an order without a shipping address.")
			: new(value);
	}

	static bool IsValidTransition(OrderStatusCode from, OrderStatusCode to) =>
		(from, to) switch
		{
			// Draft→Draft is the only allowed same-status transition (aggregate initialisation)
			(OrderStatusCode.Draft, OrderStatusCode.Draft) => true,

			(OrderStatusCode.Draft, OrderStatusCode.Confirmed) => true,
			(OrderStatusCode.Confirmed, OrderStatusCode.Shipped) => true,
			(OrderStatusCode.Shipped, OrderStatusCode.Completed) => true,

			// Any non-terminal status may be cancelled
			(not OrderStatusCode.Completed, OrderStatusCode.Cancelled) when from != OrderStatusCode.Cancelled => true,

			_ => false,
		};

	// ── Convenience factory properties (hydrate-based, no state-machine check) ──

	/// <inheritdoc cref="OrderStatusCode.Draft"/>
	public static OrderStatus Draft => Hydrate(OrderStatusCode.Draft);

	/// <inheritdoc cref="OrderStatusCode.Confirmed"/>
	public static OrderStatus Confirmed => Hydrate(OrderStatusCode.Confirmed);

	/// <inheritdoc cref="OrderStatusCode.Shipped"/>
	public static OrderStatus Shipped => Hydrate(OrderStatusCode.Shipped);

	/// <inheritdoc cref="OrderStatusCode.Completed"/>
	public static OrderStatus Completed => Hydrate(OrderStatusCode.Completed);

	/// <inheritdoc cref="OrderStatusCode.Cancelled"/>
	public static OrderStatus Cancelled => Hydrate(OrderStatusCode.Cancelled);
}
