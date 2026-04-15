namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Base <see cref="IEvent"/> implementation.
/// </summary>
public abstract class EventBase : IEvent
{
	///<inheritdoc cref="IEvent.Details"/>
	public EventDetails Details { get; init; } = new EventDetails();

	/// <summary>
	/// Gets the schema version of this event type. Increment this when the event's
	/// properties change in a breaking way so that consumers can perform version-aware
	/// deserialization or up-casting.
	/// </summary>
	/// <remarks>
	/// Defaults to 1. Override in a derived class to declare a higher version.
	/// The source generator will emit the correct override when
	/// <c>[GenerateAggregateEvent(Version = N)]</c> is used.
	/// </remarks>
	public virtual int SchemaVersion => 1;

	/// <summary>
	/// Gets a hash of the <see cref="EventBase"/>.
	/// </summary>
	/// <returns>A hash based on the name of the type, and the data.</returns>
	public override int GetHashCode()
	{
		var hashCode = new HashCode();

		hashCode.Add(GetType().FullName);
		hashCode.Add(Details);

		BuildEventHash(ref hashCode);

		return hashCode.ToHashCode();
	}

	/// <summary>
	/// Allows for <see cref="GetHashCode"/> modifications
	/// based on the payload of the <see cref="IEvent"/>.
	/// </summary>
	/// <returns>A hash based on the current events payload.</returns>
	/// <example>
	/// public class AnExampleEvent : AggregateEventBase
	/// {
	///		public string? ANullableString { get; set; }
	///
	///		public string AString { get; set; }
	///
	///		public int? ANullableInt { get; set; }
	///
	///		public int AnInt { get; set; }
	///
	///		public string[]? ANullableStringArray { get; set; }
	///
	///		public string[] AStringArray { get; set; }
	///
	///		override protected void BuildEventHash(ref HashCode hash)
	///		{
	///			hash.Add(ANullableString);
	///			hash.Add(AString);
	///			hash.Add(ANullableInt);
	///			hash.Add(ANullableString);
	///			hash.Add(AnInt);
	///
	///			BuildHash is a helper...
	///			ANullableStringArray.BuildHash(ref hash);
	///			AStringArray.BuildHash(ref hash);
	///		}
	/// }
	/// </example>
	protected abstract void BuildEventHash(ref HashCode hash);
}
