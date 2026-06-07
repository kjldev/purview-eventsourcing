using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Represents the outcome of saving a single aggregate within a transaction.
/// </summary>
public sealed class TransactionAggregateResult(
    IAggregate aggregate,
    bool saved,
    bool skipped,
    Exception? error
)
{
    /// <summary>The aggregate that was processed.</summary>
    public IAggregate Aggregate { get; } = aggregate;

    /// <summary><see langword="true"/> when the aggregate was persisted.</summary>
    public bool Saved { get; } = saved;

    /// <summary><see langword="true"/> when the save was skipped (no unsaved events, or idempotency marker matched).</summary>
    public bool Skipped { get; } = skipped;

    /// <summary>The exception that caused the save to fail, if any.</summary>
    public Exception? Error { get; } = error;
}
