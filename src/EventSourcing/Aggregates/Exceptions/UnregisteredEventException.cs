using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Exceptions;

/// <summary>
/// Indicates an <see cref="IEvent"/> was applied to an <see cref="IAggregate"/>,
/// but the event type was unregistered.
/// </summary>
public sealed class UnregisteredEventException : Exception
{
	public UnregisteredEventException() { }

	public UnregisteredEventException(string message) : base(message) { }

	public UnregisteredEventException(string message, Exception inner) : base(message, inner) { }

	public UnregisteredEventException(Type eventType, IAggregate aggregate)
		: base($"The event type '{eventType}' is not a registered event for aggregate type {aggregate}.")
	{
		EventType = eventType;
		Aggregate = aggregate;
	}

	/// <summary>
	/// The aggregate that recieved an unregistered event type.
	/// </summary>
	public IAggregate? Aggregate { get; }

	/// <summary>
	/// The type of the event.
	/// </summary>
	public Type? EventType { get; }
}
