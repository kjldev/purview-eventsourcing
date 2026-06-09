namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents details about an aggregate event, expressed through the
/// <see cref="Details"/> property.
/// </summary>
public interface IEvent
{
	/// <summary>
	/// Gets the <see cref="EventDetails"/> representing
	/// information about the event on an <see cref="IAggregate"/>.
	/// </summary>
	EventDetails Details { get; }
}
