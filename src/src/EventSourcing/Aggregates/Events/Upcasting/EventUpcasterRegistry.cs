namespace Purview.EventSourcing.Aggregates.Events.Upcasting;

/// <summary>
/// Default <see cref="IEventUpcasterRegistry"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Built from all <see cref="IEventUpcasterDescriptor"/> services registered in the DI container.
/// Descriptors are created automatically when you call
/// <c>services.AddEventUpcaster&lt;TSource, TTarget, TUpcaster&gt;()</c>.
/// </para>
/// <para>
/// <see cref="Upcast"/> follows the upcasting chain until no further upcaster is found for the
/// current event type. This means multi-hop migrations (v1 → v2 → v3) are handled transparently
/// as long as each hop is registered.
/// </para>
/// </remarks>
public sealed class EventUpcasterRegistry : IEventUpcasterRegistry
{
	readonly Dictionary<Type, IEventUpcasterDescriptor> _upcastersBySourceType;

	/// <summary>
	/// Initialises the registry.
	/// </summary>
	/// <param name="descriptors">All registered upcaster descriptors.</param>
	public EventUpcasterRegistry(IEnumerable<IEventUpcasterDescriptor> descriptors)
	{
		ArgumentNullException.ThrowIfNull(descriptors);

		// Last-registered wins when the same source type appears more than once.
		_upcastersBySourceType = descriptors.ToDictionary(d => d.SourceType);
	}

	/// <inheritdoc/>
	public bool CanUpcast(IEvent aggregateEvent)
	{
		ArgumentNullException.ThrowIfNull(aggregateEvent);
		return _upcastersBySourceType.ContainsKey(aggregateEvent.GetType());
	}

	/// <inheritdoc/>
	public IEvent Upcast(IEvent aggregateEvent)
	{
		ArgumentNullException.ThrowIfNull(aggregateEvent);

		var current = aggregateEvent;
		// Follow the chain: v1 → v2 → v3 …
		while (_upcastersBySourceType.TryGetValue(current.GetType(), out var descriptor))
			current = descriptor.Upcast(current);

		return current;
	}
}
