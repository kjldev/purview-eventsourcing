namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents an <see cref="IEvent"/> that forces
/// the saving an <see cref="IAggregate"/> when <see cref="IEventStore{T}.SaveAsync(T, EventStoreOperationContext?, CancellationToken)"/>
/// is called.
/// </summary>
public sealed class ForceSaveEvent : EventBase
{
	///<inheritdoc />
	protected override void BuildEventHash(ref HashCode hash) { }
}
