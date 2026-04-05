using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Coordinates saving multiple aggregates under a shared <see cref="CorrelationId"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation saves each aggregate sequentially via its <see cref="IEventStore{T}.SaveAsync"/>
/// method. All events share the same <see cref="CorrelationId"/>, enabling end-to-end tracing.
/// </para>
/// <para>
/// <strong>Atomicity model:</strong> If a save fails part-way through, aggregates already persisted
/// are <em>not</em> rolled back. This is an appropriate model for cross-store or cross-aggregate
/// eventual-consistency scenarios. For true ACID guarantees on SQL Server, use the
/// store-specific transaction that shares a database connection and transaction.
/// </para>
/// </remarks>
public sealed class EventStoreTransaction : IEventStoreTransaction
{
	readonly List<IEnlistedAggregate> _enlisted = [];
	bool _committed;
	bool _disposed;

	/// <summary>
	/// Creates a new transaction with the given correlation ID.
	/// </summary>
	/// <param name="correlationId">
	/// Optional correlation ID. When <see langword="null"/>, a new GUID is generated.
	/// </param>
	public EventStoreTransaction(string? correlationId = null)
	{
		CorrelationId = correlationId ?? Guid.NewGuid().ToString();
	}

	/// <inheritdoc/>
	public string CorrelationId { get; }

	/// <inheritdoc/>
	public void Enlist<T>(T aggregate, IEventStore<T> eventStore, EventStoreOperationContext? operationContext = null)
		where T : class, IAggregate, new()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_committed)
			throw new InvalidOperationException("Cannot enlist aggregates after the transaction has been committed.");

		ArgumentNullException.ThrowIfNull(aggregate);
		ArgumentNullException.ThrowIfNull(eventStore);

		_enlisted.Add(new EnlistedAggregate<T>(aggregate, eventStore, operationContext));
	}

	/// <inheritdoc/>
	public async Task<TransactionResult> CommitAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_committed)
			throw new InvalidOperationException("This transaction has already been committed.");

		_committed = true;

		var results = new List<TransactionResult.AggregateResult>(_enlisted.Count);

		foreach (var enlisted in _enlisted)
		{
			try
			{
				var saveResult = await enlisted.SaveAsync(CorrelationId, cancellationToken);
				results.Add(new TransactionResult.AggregateResult(
					enlisted.Aggregate,
					saved: saveResult.saved,
					skipped: saveResult.skipped,
					error: null
				));

				// Stop processing remaining aggregates on first failure.
				if (!saveResult.saved && !saveResult.skipped)
					break;
			}
			catch (Exception ex)
			{
				results.Add(new TransactionResult.AggregateResult(
					enlisted.Aggregate,
					saved: false,
					skipped: false,
					error: ex
				));

				// Stop on first exception — don't persist remaining aggregates.
				break;
			}
		}

		return new TransactionResult(results);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_disposed = true;
		_enlisted.Clear();

		return ValueTask.CompletedTask;
	}

	/// <summary>Non-generic handle to an enlisted aggregate.</summary>
	internal interface IEnlistedAggregate
	{
		IAggregate Aggregate { get; }

		Task<(bool saved, bool skipped)> SaveAsync(string correlationId, CancellationToken cancellationToken);
	}

	/// <summary>Closed-generic wrapper that holds the aggregate, event store, and operation context.</summary>
	sealed class EnlistedAggregate<T> : IEnlistedAggregate
		where T : class, IAggregate, new()
	{
		readonly T _aggregate;
		readonly IEventStore<T> _eventStore;
		readonly EventStoreOperationContext? _operationContext;

		public EnlistedAggregate(T aggregate, IEventStore<T> eventStore, EventStoreOperationContext? operationContext)
		{
			_aggregate = aggregate;
			_eventStore = eventStore;
			_operationContext = operationContext;
		}

		public IAggregate Aggregate => _aggregate;

		public async Task<(bool saved, bool skipped)> SaveAsync(
			string correlationId,
			CancellationToken cancellationToken
		)
		{
			var context = _operationContext ?? new EventStoreOperationContext();
			context.CorrelationId ??= correlationId;

			var result = await _eventStore.SaveAsync(_aggregate, context, cancellationToken);
			return (result.Saved, result.Skipped);
		}
	}
}
