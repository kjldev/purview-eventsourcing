using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerStepThrough]
public static class IQueryableEventStoreExtensions
{
	#region GetQueryEnumerableAsync

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.GetQueryEnumerableAsync(whereClause, null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T, TOrderBy>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, Expression<Func<T, TOrderBy>> orderByAscending, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.GetQueryEnumerableAsync(whereClause, m => m.OrderBy(orderByAscending), maxRecordsPerOperation, cancellationToken);

	#endregion GetQueryEnumerableAsync

	#region GetListEnumerableAsync

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.GetListEnumerableAsync(null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T, TOrderBy>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, TOrderBy>> orderByAscending, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.GetListEnumerableAsync(m => m.OrderBy(orderByAscending), maxRecordsPerOperation, cancellationToken);

	#endregion GetListEnumerableAsync

	#region QueryAsync

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.QueryAsync(whereClause, orderByClause, new ContinuationRequest { MaxRecords = maxRecordCount }, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, ContinuationRequest continuationRequest, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.QueryAsync(whereClause, null, continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.QueryAsync(whereClause, null, new ContinuationRequest { MaxRecords = maxRecordCount }, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, Expression<Func<T, bool>> orderByClause, ContinuationRequest continuationRequest, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.QueryAsync(whereClause, m => m.OrderBy(orderByClause), continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, Expression<Func<T, bool>> orderByClause, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.QueryAsync(whereClause, m => m.OrderBy(orderByClause), maxRecordCount, cancellationToken);

	#endregion QueryAsync

	#region ListAsync

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.ListAsync(orderByClause, new ContinuationRequest { MaxRecords = maxRecordCount }, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, ContinuationRequest continuationRequest, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.ListAsync(null, continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.ListAsync(null, new ContinuationRequest { MaxRecords = maxRecordCount }, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> orderByClause, ContinuationRequest continuationRequest, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.ListAsync(m => m.OrderBy(orderByClause), continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> orderByClause, int maxRecordCount = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.ListAsync(m => m.OrderBy(orderByClause), maxRecordCount, cancellationToken);

	#endregion ListAsync

	#region CountAsync

	public static Task<long> CountAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.CountAsync(null, cancellationToken);

	#endregion CountAsync

	#region FirstOrDefaultAsync

	public static Task<T?> FirstOrDefaultAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.FirstOrDefaultAsync(whereClause, null, cancellationToken);

	public static Task<T?> FirstOrDefaultAsync<T>([NotNull] this IQueryableEventStore<T> eventStore, Expression<Func<T, bool>> whereClause, Expression<Func<T, bool>> orderByClause, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new()
		=> eventStore.FirstOrDefaultAsync(whereClause, m => m.OrderBy(orderByClause), cancellationToken);

	#endregion FirstOrDefaultAsync
}
