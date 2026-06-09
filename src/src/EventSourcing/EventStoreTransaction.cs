using System.Data.Common;
using System.Diagnostics;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing;

/// <summary>
/// Coordinates saving multiple aggregates under a shared <see cref="CorrelationId"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation automatically selects the strongest compatible transaction coordinator for the
/// enlisted stores. When all enlisted stores share the same native transactional boundary, they are
/// committed atomically through that provider-specific coordinator. Otherwise, the transaction falls
/// back to sequential <see cref="IEventStore.SaveAsync{T}(T, EventStoreOperationContext?, CancellationToken)"/> calls under a shared <see cref="CorrelationId"/>.
/// </para>
/// <para>
/// <strong>Atomicity model:</strong> when provider-native coordination is unavailable or the enlisted
/// stores do not share the same transaction boundary, previously persisted aggregates are <em>not</em>
/// rolled back. This preserves the existing logical transaction semantics for mixed-provider or
/// unsupported scenarios.
/// </para>
/// </remarks>
/// <remarks>
/// Creates a new transaction with the given correlation ID.
/// </remarks>
public sealed class EventStoreTransaction : IEventStoreTransaction
{
	readonly List<IEnlistedAggregate> _enlisted = [];
	readonly bool _useIdempotencyMarker;
	bool _committed;
	bool _disposed;

	public EventStoreTransaction(string? correlationId = null)
	{
		var resolvedCorrelationId = correlationId ?? Activity.Current?.Id;
		CorrelationId = resolvedCorrelationId ?? Guid.NewGuid().ToString();
		_useIdempotencyMarker = !string.IsNullOrWhiteSpace(resolvedCorrelationId);
	}

	/// <inheritdoc/>
	public string CorrelationId { get; }

	/// <inheritdoc/>
	public void Enlist<T>(T aggregate, IEventStore eventStore, EventStoreOperationContext? operationContext = null)
		where T : class, IAggregate, new()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_committed)
			throw new InvalidOperationException("Cannot enlist aggregates after the transaction has been committed.");

		ArgumentNullException.ThrowIfNull(aggregate);
		ArgumentNullException.ThrowIfNull(eventStore);

		_enlisted.Add(new EnlistedAggregate<T>(aggregate, eventStore, operationContext, _useIdempotencyMarker));
	}

	/// <inheritdoc/>
	public void Enlist<T>(
		T aggregate,
		IEventStoreCore<T> eventStore,
		EventStoreOperationContext? operationContext = null
	)
		where T : class, IAggregate, new()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_committed)
			throw new InvalidOperationException("Cannot enlist aggregates after the transaction has been committed.");

		ArgumentNullException.ThrowIfNull(aggregate);
		ArgumentNullException.ThrowIfNull(eventStore);

		_enlisted.Add(new EnlistedAggregate<T>(aggregate, eventStore, operationContext, _useIdempotencyMarker));
	}

	/// <inheritdoc/>
	public async Task<TransactionResult> CommitAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_committed)
			throw new InvalidOperationException("This transaction has already been committed.");

		_committed = true;

		return _enlisted.Count == 0 ? new TransactionResult([])
			: CanUseNativeTransactionCoordinator() ? await CommitWithNativeTransactionAsync(cancellationToken)
			: await CommitSequentiallyAsync(cancellationToken);
	}

	bool CanUseNativeTransactionCoordinator()
	{
		string? boundaryKey = null;

		foreach (var enlisted in _enlisted)
		{
			if (string.IsNullOrWhiteSpace(enlisted.TransactionBoundaryKey))
				return false;

			if (boundaryKey is null)
			{
				boundaryKey = enlisted.TransactionBoundaryKey;
				continue;
			}

			if (!StringComparer.Ordinal.Equals(boundaryKey, enlisted.TransactionBoundaryKey))
				return false;
		}

		return boundaryKey is not null;
	}

	async Task<TransactionResult> CommitSequentiallyAsync(CancellationToken cancellationToken)
	{
		var results = new List<TransactionAggregateResult>(_enlisted.Count);

		foreach (var enlisted in _enlisted)
		{
			try
			{
				var (saved, skipped) = await enlisted.SaveAsync(CorrelationId, cancellationToken);
				results.Add(
					new TransactionAggregateResult(enlisted.Aggregate, saved: saved, skipped: skipped, error: null)
				);

				// Stop processing remaining aggregates on first failure.
				if (!saved && !skipped)
					break;
			}
#pragma warning disable CA1031
			catch (Exception ex)
#pragma warning restore CA1031
			{
				results.Add(
					new TransactionAggregateResult(enlisted.Aggregate, saved: false, skipped: false, error: ex)
				);

				// Stop on first exception — don't persist remaining aggregates.
				break;
			}
		}

		return new TransactionResult(results);
	}

	async Task<TransactionResult> CommitWithNativeTransactionAsync(CancellationToken cancellationToken)
	{
		await using var connection = _enlisted[0].CreateTransactionConnection();
		await connection.OpenAsync(cancellationToken);

		foreach (var enlisted in _enlisted)
			await enlisted.EnsureTransactionConfiguredAsync(connection, cancellationToken);

		await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

		var processed = new List<IProcessedSaveOperation>(_enlisted.Count);
		IEnlistedAggregate? failedEnlisted = null;
		Exception? failure = null;

		foreach (var enlisted in _enlisted)
		{
			try
			{
				var operation = await enlisted.SaveInTransactionAsync(
					CorrelationId,
					connection,
					transaction,
					cancellationToken
				);

				processed.Add(operation);

				if (!operation.Saved && !operation.Skipped)
				{
					failedEnlisted = enlisted;
					break;
				}
			}
#pragma warning disable CA1031
			catch (Exception ex)
#pragma warning restore CA1031
			{
				failedEnlisted = enlisted;
				failure = ex;
				break;
			}
		}

		if (failedEnlisted is null)
		{
			await transaction.CommitAsync(cancellationToken);

			var committedResults = new List<TransactionAggregateResult>(processed.Count);
			foreach (var operation in processed)
			{
				Exception? postCommitError = null;
				try
				{
					await operation.AfterCommitAsync(cancellationToken);
				}
#pragma warning disable CA1031
				catch (Exception ex)
#pragma warning restore CA1031
				{
					postCommitError = ex;
				}

				committedResults.Add(
					new TransactionAggregateResult(
						operation.Aggregate,
						operation.Saved,
						operation.Skipped,
						postCommitError
					)
				);
			}

			return new TransactionResult(committedResults);
		}

		await transaction.RollbackAsync(cancellationToken);

		foreach (var operation in processed)
			await operation.AfterRollbackAsync(cancellationToken);

		var rollbackResults = new List<TransactionAggregateResult>(processed.Count + 1);
		var rollbackError =
			failure
			?? new InvalidOperationException(
				"The transaction was rolled back because an enlisted aggregate could not be saved."
			);

		foreach (var operation in processed)
		{
			var isFailedOperation =
				failure is null
				&& ReferenceEquals(operation.Aggregate, failedEnlisted.Aggregate)
				&& !operation.Saved
				&& !operation.Skipped;
			var wasRolledBack = operation.Saved;

			rollbackResults.Add(
				new TransactionAggregateResult(
					operation.Aggregate,
					saved: false,
					skipped: !isFailedOperation && !wasRolledBack && operation.Skipped,
					error: isFailedOperation || operation.Skipped ? null : rollbackError
				)
			);
		}

		if (
			failure is not null
			&& processed.All(operation => !ReferenceEquals(operation.Aggregate, failedEnlisted.Aggregate))
		)
		{
			rollbackResults.Add(
				new TransactionAggregateResult(failedEnlisted.Aggregate, saved: false, skipped: false, error: failure)
			);
		}

		return new TransactionResult(rollbackResults);
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
		string? TransactionBoundaryKey { get; }

		Task<(bool saved, bool skipped)> SaveAsync(string correlationId, CancellationToken cancellationToken);

		DbConnection CreateTransactionConnection();

		Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken cancellationToken);

		Task<IProcessedSaveOperation> SaveInTransactionAsync(
			string correlationId,
			DbConnection connection,
			DbTransaction transaction,
			CancellationToken cancellationToken
		);
	}

	internal interface IProcessedSaveOperation
	{
		IAggregate Aggregate { get; }
		bool Saved { get; }
		bool Skipped { get; }
		Task AfterCommitAsync(CancellationToken cancellationToken);
		Task AfterRollbackAsync(CancellationToken cancellationToken);
	}

	/// <summary>Closed-generic wrapper that holds the aggregate, event store, and operation context.</summary>
	sealed class EnlistedAggregate<T> : IEnlistedAggregate
		where T : class, IAggregate, new()
	{
		readonly T _aggregate;
		readonly IEventStore? _eventStore;
		readonly IEventStoreCore<T>? _eventStoreImpl;
		readonly ITransactionalEventStore<T>? _transactionalEventStore;
		readonly EventStoreOperationContext? _operationContext;
		readonly bool _useIdempotencyMarker;

		public EnlistedAggregate(
			T aggregate,
			IEventStore eventStore,
			EventStoreOperationContext? operationContext,
			bool useIdempotencyMarker
		)
		{
			_aggregate = aggregate;
			_eventStore = eventStore;
			_eventStoreImpl = (eventStore as IEventStoreImplementationAccessor)?.GetEventStore<T>();
			_transactionalEventStore = _eventStoreImpl as ITransactionalEventStore<T>;
			_operationContext = operationContext;
			_useIdempotencyMarker = useIdempotencyMarker;
		}

		public EnlistedAggregate(
			T aggregate,
			IEventStoreCore<T> eventStore,
			EventStoreOperationContext? operationContext,
			bool useIdempotencyMarker
		)
		{
			_aggregate = aggregate;
			_eventStore = null;
			_eventStoreImpl = eventStore;
			_transactionalEventStore = eventStore as ITransactionalEventStore<T>;
			_operationContext = operationContext;
			_useIdempotencyMarker = useIdempotencyMarker;
		}

		public IAggregate Aggregate => _aggregate;
		public string? TransactionBoundaryKey => _transactionalEventStore?.TransactionBoundaryKey;

		public async Task<(bool saved, bool skipped)> SaveAsync(
			string correlationId,
			CancellationToken cancellationToken
		)
		{
			var baseContext = _operationContext ?? EventStoreOperationContext.DefaultContext;
			var context = baseContext with
			{
				CorrelationId = baseContext.CorrelationId ?? correlationId,
				UseIdempotencyMarker =
					baseContext.UseIdempotencyMarker
					|| _useIdempotencyMarker
					|| !string.IsNullOrWhiteSpace(baseContext.CorrelationId),
			};

			var result = _eventStoreImpl is not null
				? await _eventStoreImpl.SaveAsync(_aggregate, context, cancellationToken)
				: await _eventStore!.SaveAsync(_aggregate, context, cancellationToken);
			return (result.Saved, result.Skipped);
		}

		public DbConnection CreateTransactionConnection() =>
			_transactionalEventStore?.CreateTransactionConnection()
			?? throw new InvalidOperationException(
				$"The enlisted event store '{_eventStoreImpl?.GetType().FullName ?? _eventStore?.GetType().FullName}' does not support native transactions."
			);

		public Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken cancellationToken) =>
			_transactionalEventStore?.EnsureTransactionConfiguredAsync(connection, cancellationToken)
			?? throw new InvalidOperationException(
				$"The enlisted event store '{_eventStoreImpl?.GetType().FullName ?? _eventStore?.GetType().FullName}' does not support native transactions."
			);

		public async Task<IProcessedSaveOperation> SaveInTransactionAsync(
			string correlationId,
			DbConnection connection,
			DbTransaction transaction,
			CancellationToken cancellationToken
		)
		{
			if (_transactionalEventStore is null)
			{
				throw new InvalidOperationException(
					$"The enlisted event store '{_eventStoreImpl?.GetType().FullName ?? _eventStore?.GetType().FullName}' does not support native transactions."
				);
			}

			var baseContext = _operationContext ?? EventStoreOperationContext.DefaultContext;
			var context = baseContext with
			{
				CorrelationId = baseContext.CorrelationId ?? correlationId,
				UseIdempotencyMarker =
					baseContext.UseIdempotencyMarker
					|| _useIdempotencyMarker
					|| !string.IsNullOrWhiteSpace(baseContext.CorrelationId),
			};

			var saveOperation = await _transactionalEventStore.SaveInTransactionAsync(
				_aggregate,
				context,
				connection,
				transaction,
				cancellationToken
			);

			return new ProcessedSaveOperation<T>(_aggregate, saveOperation);
		}
	}

	sealed class ProcessedSaveOperation<T>(T aggregate, TransactionalSaveOperation<T> operation)
		: IProcessedSaveOperation
		where T : class, IAggregate, new()
	{
		public IAggregate Aggregate => aggregate;
		public bool Saved => operation.Result.Saved;
		public bool Skipped => operation.Result.Skipped;

		public Task AfterCommitAsync(CancellationToken cancellationToken) =>
			operation.AfterCommitAsync(cancellationToken);

		public Task AfterRollbackAsync(CancellationToken cancellationToken) =>
			operation.AfterRollbackAsync(cancellationToken);
	}
}
