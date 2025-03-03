namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents a <see cref="IEvent"/> that is used in-place
/// when an existing event could not be deserialized.
/// </summary>
/// <remarks>
/// <para>
/// This can occur when the aggregate's schema has changed
/// and it no long requires an event type, so it's removed.
/// </para>
/// <para>However, the event data still exists in the underlying store.</para>
/// </remarks>
public sealed class UnknownEvent : EventBase
{
	/// <summary>
	/// Represents the serialized payload that was the
	/// <see cref="IEvent"/>.
	/// </summary>
	public string? Payload { get; set; }

	///<inheritdoc/>
	protected override void BuildEventHash(ref HashCode hash)
		=> hash.Add(Payload);
}
