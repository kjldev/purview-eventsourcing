namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents an <see cref="IEvent"/> that tracks
/// the restoring of an <see cref="IAggregate"/> following a soft delete.
/// </summary>
public sealed class RestoreEvent : EventBase
{
	///<inheritdoc/>
	protected override void BuildEventHash(ref HashCode hash) { }
}
