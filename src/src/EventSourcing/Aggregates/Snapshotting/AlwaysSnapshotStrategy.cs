namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// A snapshot strategy that writes a snapshot on every save that actually persisted at least
/// one event.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public sealed class AlwaysSnapshotStrategy<T> : ISnapshotStrategy<T>
    where T : class, IAggregate, new()
{
    /// <inheritdoc/>
    public bool ShouldSnapshot(T aggregate, int eventsApplied) => eventsApplied > 0;
}
