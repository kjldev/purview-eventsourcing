using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Summarises the outcome of an <see cref="IEventStoreTransaction.CommitAsync"/> call.
/// </summary>
public sealed class TransactionResult(List<TransactionResult.AggregateResult> results)
{
	/// <summary>
	/// The individual save results for each enlisted aggregate, in the order they were enlisted.
	/// </summary>
	public IReadOnlyList<AggregateResult> Results => results;

	/// <summary>
	/// <see langword="true"/> when at least one aggregate was enlisted and every enlisted aggregate was persisted.
	/// Returns <see langword="false"/> for empty transactions and when any aggregate was skipped or failed.
	/// </summary>
	public bool Success => results.Count > 0 && results.TrueForAll(r => r.Saved);

	/// <summary>
	/// <see langword="true"/> when the transaction completed without any save failures.
	/// This includes empty transactions and transactions where aggregates were skipped rather than persisted.
	/// </summary>
	public bool CompletedWithoutError => results.TrueForAll(r => r.Saved || r.Skipped);

	/// <summary>
	/// Represents the outcome of saving a single aggregate within a transaction.
	/// </summary>
	public sealed class AggregateResult(IAggregate aggregate, bool saved, bool skipped, Exception? error)
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
}
