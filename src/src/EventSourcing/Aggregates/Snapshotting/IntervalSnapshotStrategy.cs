namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// A snapshot strategy that writes a snapshot every <em>N</em> events.
/// </summary>
/// <typeparam name="T">The aggregate type.</typeparam>
/// <remarks>
/// <para>
/// The snapshot is written when <c>aggregate.Details.SavedVersion % <see cref="Interval"/> == 0</c>
/// (i.e. the saved version is a multiple of the interval). This mirrors the behaviour of the
/// legacy <c>SnapshotInterval</c> configuration option.
/// </para>
/// <para>
/// A value of <c>1</c> means every save triggers a snapshot (same as
/// <see cref="AlwaysSnapshotStrategy{T}"/>). Set a higher value such as <c>50</c> to reduce
/// snapshot write amplification for high-frequency aggregates.
/// </para>
/// </remarks>
public sealed class IntervalSnapshotStrategy<T> : ISnapshotStrategy<T>
    where T : class, IAggregate, new()
{
    /// <summary>
    /// The snapshot interval. Defaults to <c>1</c> (snapshot on every save).
    /// </summary>
    public int Interval { get; }

    /// <summary>
    /// Initialises the strategy with the specified <paramref name="interval"/>.
    /// </summary>
    /// <param name="interval">The number of events between snapshots. Must be ≥ 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="interval"/> is less than 1.</exception>
    public IntervalSnapshotStrategy(int interval = 1)
    {
        if (interval < 1)
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                interval,
                "Snapshot interval must be at least 1."
            );

        Interval = interval;
    }

    /// <inheritdoc/>
    public bool ShouldSnapshot(T aggregate, int eventsApplied)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        return eventsApplied > 0 && aggregate.Details.SavedVersion % Interval == 0;
    }
}
