//using System.Data.Common;
//using Purview.EventSourcing.Aggregates;
//using Purview.EventSourcing.Internal;

//namespace Purview.EventSourcing.SqlServer.Events;

//sealed class SqlServerEventStoreTransaction(string? correlationId = null) : IEventStoreTransaction
//{
//	readonly List<IEnlistedAggregate> _enlisted = [];
//	bool _committed;
//	bool _disposed;

//	public string CorrelationId { get; } = correlationId ?? Guid.NewGuid().ToString();

//	public void Enlist<T>(T aggregate, IEventStore eventStore, EventStoreOperationContext? operationContext = null)
//		where T : class, IAggregate, new()
//	{
//		ObjectDisposedException.ThrowIf(_disposed, this);

//		if (_committed)
//			throw new InvalidOperationException("Cannot enlist aggregates after the transaction has been committed.");

//		ArgumentNullException.ThrowIfNull(aggregate);
//		ArgumentNullException.ThrowIfNull(eventStore);

//		if (
//			(eventStore as IEventStoreImplementationAccessor)?.GetEventStore<T>()
//			is not ITransactionalEventStore<T> transactionalEventStore
//		)
//		{
//			throw new InvalidOperationException(
//				$"The enlisted event store '{eventStore.GetType().FullName}' does not support atomic SQL Server transactions."
//			);
//		}

//		_enlisted.Add(new EnlistedAggregate<T>(aggregate, transactionalEventStore, operationContext));
//	}

//	public void Enlist<T>(
//		T aggregate,
//		IEventStoreCore<T> eventStore,
//		EventStoreOperationContext? operationContext = null
//	)
//		where T : class, IAggregate, new()
//	{
//		ObjectDisposedException.ThrowIf(_disposed, this);

//		if (_committed)
//			throw new InvalidOperationException("Cannot enlist aggregates after the transaction has been committed.");

//		ArgumentNullException.ThrowIfNull(aggregate);
//		ArgumentNullException.ThrowIfNull(eventStore);

//		if (eventStore is not ITransactionalEventStore<T> transactionalEventStore)
//		{
//			throw new InvalidOperationException(
//				$"The enlisted event store '{eventStore.GetType().FullName}' does not support atomic SQL Server transactions."
//			);
//		}

//		_enlisted.Add(new EnlistedAggregate<T>(aggregate, transactionalEventStore, operationContext));
//	}

//	public async Task<TransactionResult> CommitAsync(CancellationToken cancellationToken = default)
//	{
//		ObjectDisposedException.ThrowIf(_disposed, this);

//		if (_committed)
//			throw new InvalidOperationException("This transaction has already been committed.");

//		_committed = true;

//		if (_enlisted.Count == 0)
//			return new TransactionResult([]);

//		await using var connection = _enlisted[0].CreateTransactionConnection();
//		await connection.OpenAsync(cancellationToken);

//		foreach (var enlisted in _enlisted)
//			await enlisted.EnsureTransactionConfiguredAsync(connection, cancellationToken);

//		await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

//		List<IProcessedSaveOperation> processed = [with(_enlisted.Count)];
//		IEnlistedAggregate? failedEnlisted = null;
//		Exception? failure = null;

//		foreach (var enlisted in _enlisted)
//		{
//#pragma warning disable CA1031 // Do not catch general exception types
//			try
//			{
//				var operation = await enlisted.SaveInTransactionAsync(
//					CorrelationId,
//					connection,
//					transaction,
//					cancellationToken
//				);

//				processed.Add(operation);

//				if (!operation.Saved && !operation.Skipped)
//				{
//					failedEnlisted = enlisted;
//					break;
//				}
//			}
//			catch (Exception ex)
//			{
//				failedEnlisted = enlisted;
//				failure = ex;
//				break;
//			}
//#pragma warning restore CA1031 // Do not catch general exception types
//		}

//		if (failedEnlisted is null)
//		{
//			await transaction.CommitAsync(cancellationToken);

//			List<TransactionAggregateResult> committedResults = [with(processed.Count)];
//			foreach (var operation in processed)
//			{
//				Exception? postCommitError = null;
//#pragma warning disable CA1031 // Do not catch general exception types
//				try
//				{
//					await operation.AfterCommitAsync(cancellationToken);
//				}
//				catch (Exception ex)
//				{
//					postCommitError = ex;
//				}
//#pragma warning restore CA1031 // Do not catch general exception types

//				committedResults.Add(new(operation.Aggregate, operation.Saved, operation.Skipped, postCommitError));
//			}

//			return new TransactionResult(committedResults);
//		}

//		await transaction.RollbackAsync(cancellationToken);

//		foreach (var operation in processed)
//			await operation.AfterRollbackAsync(cancellationToken);

//		var rollbackResults = new List<TransactionAggregateResult>(processed.Count + 1);
//		var rollbackError =
//			failure
//			?? new InvalidOperationException(
//				"The transaction was rolled back because an enlisted aggregate could not be saved."
//			);

//		foreach (var operation in processed)
//		{
//			var isFailedOperation =
//				failure is null
//				&& ReferenceEquals(operation.Aggregate, failedEnlisted.Aggregate)
//				&& !operation.Saved
//				&& !operation.Skipped;
//			var wasRolledBack = operation.Saved;

//			rollbackResults.Add(
//				new TransactionAggregateResult(
//					operation.Aggregate,
//					saved: false,
//					skipped: !isFailedOperation && !wasRolledBack && operation.Skipped,
//					error: isFailedOperation || operation.Skipped ? null : rollbackError
//				)
//			);
//		}

//		if (
//			failure is not null
//			&& processed.All(operation => !ReferenceEquals(operation.Aggregate, failedEnlisted.Aggregate))
//		)
//		{
//			rollbackResults.Add(
//				new TransactionAggregateResult(failedEnlisted.Aggregate, saved: false, skipped: false, error: failure)
//			);
//		}

//		return new TransactionResult(rollbackResults);
//	}

//	public ValueTask DisposeAsync()
//	{
//		_disposed = true;
//		_enlisted.Clear();

//		return ValueTask.CompletedTask;
//	}

//	interface IEnlistedAggregate
//	{
//		IAggregate Aggregate { get; }

//		DbConnection CreateTransactionConnection();

//		Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken cancellationToken);

//		Task<IProcessedSaveOperation> SaveInTransactionAsync(
//			string correlationId,
//			DbConnection connection,
//			DbTransaction transaction,
//			CancellationToken cancellationToken
//		);
//	}

//	interface IProcessedSaveOperation
//	{
//		IAggregate Aggregate { get; }
//		bool Saved { get; }
//		bool Skipped { get; }
//		Task AfterCommitAsync(CancellationToken cancellationToken);
//		Task AfterRollbackAsync(CancellationToken cancellationToken);
//	}

//	sealed class EnlistedAggregate<T>(
//		T aggregate,
//		ITransactionalEventStore<T> eventStore,
//		EventStoreOperationContext? operationContext
//	) : IEnlistedAggregate
//		where T : class, IAggregate, new()
//	{
//		public IAggregate Aggregate => aggregate;

//		public DbConnection CreateTransactionConnection() => eventStore.CreateTransactionConnection();

//		public Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken cancellationToken) =>
//			eventStore.EnsureTransactionConfiguredAsync(connection, cancellationToken);

//		public async Task<IProcessedSaveOperation> SaveInTransactionAsync(
//			string correlationId,
//			DbConnection connection,
//			DbTransaction transaction,
//			CancellationToken cancellationToken
//		)
//		{
//			var baseContext = operationContext ?? EventStoreOperationContext.DefaultContext;
//			var context = baseContext with { CorrelationId = baseContext.CorrelationId ?? correlationId };

//			var saveOperation = await eventStore.SaveInTransactionAsync(
//				aggregate,
//				context,
//				connection,
//				transaction,
//				cancellationToken
//			);

//			return new ProcessedSaveOperation<T>(aggregate, saveOperation);
//		}
//	}

//	sealed class ProcessedSaveOperation<T>(T aggregate, TransactionalSaveOperation<T> operation)
//		: IProcessedSaveOperation
//		where T : class, IAggregate, new()
//	{
//		public IAggregate Aggregate => aggregate;
//		public bool Saved => operation.Result.Saved;
//		public bool Skipped => operation.Result.Skipped;

//		public Task AfterCommitAsync(CancellationToken cancellationToken) =>
//			operation.AfterCommitAsync(cancellationToken);

//		public Task AfterRollbackAsync(CancellationToken cancellationToken) =>
//			operation.AfterRollbackAsync(cancellationToken);
//	}
//}
