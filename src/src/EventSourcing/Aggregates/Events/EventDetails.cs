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
    /// <para>The id of the event that caused this event to be raised, enabling causation chain tracking.</para>
    /// <para>
    /// Set this to the <see cref="IdempotencyId"/> (or a stable identifier) of the upstream event
    /// that triggered this operation. Together with <see cref="CorrelationId"/> this allows a
    /// full distributed-trace of cause-and-effect across aggregate boundaries.
    /// </para>
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// <para>A correlation identifier that groups a set of causally-related events.</para>
    /// <para>
    /// Typically sourced from the <see cref="EventStoreOperationContext.CorrelationId"/> of the
    /// originating operation. Propagate the same value across all aggregates that participate in the
    /// same logical request or workflow so that events can be correlated end-to-end.
    /// </para>
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Generates a hash-code based on the properties of the <see cref="EventDetails"/>.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() =>
        HashCode.Combine(IdempotencyId, AggregateVersion, When, UserId);
}
