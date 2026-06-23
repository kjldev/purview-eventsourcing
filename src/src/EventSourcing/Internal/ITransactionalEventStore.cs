using System.ComponentModel;
using System.Data.Common;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITransactionalEventStore<T> : IEventStoreCore<T>
	where T : class, IAggregate, new()
{
	string TransactionBoundaryKey { get; }

	DbConnection CreateTransactionConnection();

	Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken cancellationToken = default);

	Task<TransactionalSaveOperation<T>> SaveInTransactionAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		DbConnection connection,
		DbTransaction transaction,
		CancellationToken cancellationToken = default
	);
}
