namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents an <see cref="IEvent"/> that tracks
/// the soft-deleting of an <see cref="IAggregate"/>.
/// </summary>
public sealed class DeleteEvent : EventBase
{
	///<inheritdoc />
	protected override void BuildEventHash(ref HashCode hash) { }
}
