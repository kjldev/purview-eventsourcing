using System.ComponentModel;
using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Provider-facing typed queryable event-store contract used by concrete implementations and internal infrastructure.
/// </summary>
/// <typeparam name="T">An <see cref="IAggregate"/> implementation.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IQueryableEventStoreImpl<T> : IEventStoreImpl<T>
	where T : class, IAggregate, new()
{
	IAsyncEnumerable<T> GetQueryEnumerableAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<T> GetListEnumerableAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	);

	Task<ContinuationResponse<T>> QueryAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	);

	Task<ContinuationResponse<T>> ListAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	);

	Task<long> CountAsync(Expression<Func<T, bool>>? whereClause, CancellationToken cancellationToken = default);

	Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> whereClause, CancellationToken cancellationToken = default);

	Task<T?> FirstOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		CancellationToken cancellationToken = default
	);
}
