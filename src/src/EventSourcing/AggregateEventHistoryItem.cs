namespace Purview.EventSourcing;

/// <summary>
/// A single event-history entry for an aggregate.
/// </summary>
public sealed class AggregateEventHistoryItem
{
	public string AggregateId { get; set; } = default!;

	public string AggregateType { get; set; } = default!;

	public string EventType { get; set; } = default!;

	public string EventClrType { get; set; } = default!;

	public int AggregateVersion { get; set; }

	public DateTimeOffset When { get; set; }

	public string? IdempotencyId { get; set; }

	public string? UserId { get; set; }

	public string? CausationId { get; set; }

	public string? CorrelationId { get; set; }

	public bool IsUnknownEvent { get; set; }

	public string? Payload { get; set; }
}
