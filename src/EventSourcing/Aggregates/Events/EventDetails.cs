namespace Purview.EventSourcing.Aggregates.Events;

/// <summary>
/// Represents details about the <see cref="IEvent"/>.
/// </summary>
public sealed class EventDetails
{
	/// <summary>
	/// The idempotency Id for the operation that resulted in this event being applied.
	/// </summary>
	public string? IdempotencyId { get; set; }

	/// <summary>
	/// The version the of the aggregate at the
	/// time the event was applied.
	/// </summary>
	public int AggregateVersion { get; set; }

	/// <summary>
	/// The <see cref="DateTimeOffset.UtcNow">UTC date/time</see>
	/// the owning event was applied.
	/// </summary>
	public DateTimeOffset When { get; set; }

	/// <summary>
	/// The id of the user that applied this event.
	/// </summary>
	public string? UserId { get; set; }

	/// <summary>
	/// Generates a hash-code based on the properties of the <see cref="EventDetails"/>.
	/// </summary>
	/// <returns></returns>
	public override int GetHashCode()
		=> HashCode.Combine(
			IdempotencyId,
			AggregateVersion,
			When,
			UserId
		);
}
