using System.ComponentModel;
using System.Data.Common;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITransactionalEventStore<T> : IEventStore<T>
	where T : class, IAggregate, new()
{
	DbConnection CreateTransactionConnection();

	Task EnsureTransactionConfiguredAsync(
		DbConnection connection,
		CancellationToken cancellationToken = default
	);

	Task<TransactionalSaveOperation<T>> SaveInTransactionAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		DbConnection connection,
		DbTransaction transaction,
		CancellationToken cancellationToken = default
	);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TransactionalSaveOperation<T>(
	SaveResult<T> result,
	Func<CancellationToken, Task>? afterCommit = null,
	Func<CancellationToken, Task>? afterRollback = null
)
	where T : class, IAggregate, new()
{
	static readonly Func<CancellationToken, Task> NoOp = _ => Task.CompletedTask;

	public SaveResult<T> Result { get; } = result;

	readonly Func<CancellationToken, Task> _afterCommit = afterCommit ?? NoOp;
	readonly Func<CancellationToken, Task> _afterRollback = afterRollback ?? NoOp;

	public Task AfterCommitAsync(CancellationToken cancellationToken = default) =>
		_afterCommit(cancellationToken);

	public Task AfterRollbackAsync(CancellationToken cancellationToken = default) =>
		_afterRollback(cancellationToken);
}
