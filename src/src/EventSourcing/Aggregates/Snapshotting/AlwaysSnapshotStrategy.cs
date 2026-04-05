namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// A snapshot strategy that writes a snapshot on every save, regardless of how many events
/// were applied.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
public sealed class AlwaysSnapshotStrategy<T> : ISnapshotStrategy<T>
	where T : class, IAggregate, new()
{
	/// <inheritdoc/>
	public bool ShouldSnapshot(T aggregate, int eventsApplied) => eventsApplied > 0;
}
