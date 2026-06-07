namespace Purview.EventSourcing;

/// <summary>
/// Summarises the outcome of an <see cref="IEventStoreTransaction.CommitAsync"/> call.
/// </summary>
public sealed partial class TransactionResult(IReadOnlyList<TransactionAggregateResult> results)
{
    /// <summary>
    /// The individual save results for each enlisted aggregate, in the order they were enlisted.
    /// </summary>
    public IReadOnlyList<TransactionAggregateResult> Results => results;

    /// <summary>
    /// <see langword="true"/> when at least one aggregate was enlisted and every enlisted aggregate was persisted.
    /// Returns <see langword="false"/> for empty transactions and when any aggregate was skipped or failed.
    /// </summary>
    public bool Success => results.Count > 0 && results.All(r => r.Saved);

    /// <summary>
    /// <see langword="true"/> when the transaction completed without any save failures.
    /// This includes empty transactions and transactions where aggregates were skipped rather than persisted.
    /// </summary>
    public bool CompletedWithoutError => results.All(r => r.Saved || r.Skipped);
}
