namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// Determines when a snapshot of <typeparamref name="T"/> should be written to the store
/// during a save operation.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
/// <remarks>
/// <para>
/// The framework calls <see cref="ShouldSnapshot"/> after events have been persisted.
/// When the method returns <see langword="true"/>, a snapshot is written so that future
/// reads can skip replaying the full event history.
/// </para>
/// <para>
/// Register your implementation with DI to replace the default behaviour. If no implementation
/// is registered, the default <see cref="IntervalSnapshotStrategy{T}"/> is used, which
/// mirrors the existing <c>SnapshotInterval</c> configuration setting.
/// </para>
/// <para>Built-in implementations:</para>
/// <list type="bullet">
///   <item><see cref="IntervalSnapshotStrategy{T}"/> — snapshot every <em>N</em> events.</item>
///   <item><see cref="AlwaysSnapshotStrategy{T}"/> — snapshot on every save.</item>
///   <item><see cref="NeverSnapshotStrategy{T}"/> — never write a snapshot (event-sourcing only).</item>
/// </list>
/// </remarks>
public interface ISnapshotStrategy<T>
    where T : class, IAggregate, new()
{
    /// <summary>
    /// Returns <see langword="true"/> when a snapshot should be written after saving
    /// <paramref name="aggregate"/>.
    /// </summary>
    /// <param name="aggregate">
    /// The aggregate that was just saved, with <see cref="AggregateDetails.SavedVersion"/>
    /// already updated to the new version.
    /// </param>
    /// <param name="eventsApplied">
    /// The number of events that were persisted in this save operation.
    /// </param>
    bool ShouldSnapshot(T aggregate, int eventsApplied);
}
