using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Summarises the outcome of an <see cref="IEventStoreTransaction.CommitAsync"/> call.
/// </summary>
public sealed class TransactionResult
{
	readonly List<AggregateResult> _results;

	internal TransactionResult(List<AggregateResult> results)
	{
		_results = results;
	}

	/// <summary>
	/// The individual save results for each enlisted aggregate, in the order they were enlisted.
	/// </summary>
	public IReadOnlyList<AggregateResult> Results => _results;

	/// <summary>
	/// <see langword="true"/> when every enlisted aggregate saved successfully.
	/// </summary>
	public bool Success => _results.Count > 0 && _results.TrueForAll(r => r.Saved);

	/// <summary>
	/// Represents the outcome of saving a single aggregate within a transaction.
	/// </summary>
	public sealed class AggregateResult
	{
		internal AggregateResult(IAggregate aggregate, bool saved, bool skipped, Exception? error)
		{
			Aggregate = aggregate;
			Saved = saved;
			Skipped = skipped;
			Error = error;
		}

		/// <summary>The aggregate that was processed.</summary>
		public IAggregate Aggregate { get; }

		/// <summary><see langword="true"/> when the aggregate was persisted.</summary>
		public bool Saved { get; }

		/// <summary><see langword="true"/> when the save was skipped (no unsaved events, or idempotency marker matched).</summary>
		public bool Skipped { get; }

		/// <summary>The exception that caused the save to fail, if any.</summary>
		public Exception? Error { get; }
	}
}
