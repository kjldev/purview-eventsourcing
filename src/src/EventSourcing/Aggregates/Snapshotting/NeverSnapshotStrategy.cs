namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// A snapshot strategy that never writes a snapshot.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
/// <remarks>
/// Use this in scenarios where you want pure event-sourcing without any snapshot optimisation,
/// or during development/testing where snapshot noise is undesirable.
/// </remarks>
public sealed class NeverSnapshotStrategy<T> : ISnapshotStrategy<T>
    where T : class, IAggregate, new()
{
    /// <inheritdoc/>
    public bool ShouldSnapshot(T aggregate, int eventsApplied) => false;
}
