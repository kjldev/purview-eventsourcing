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

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetQueryEnumerableAsync<T>(whereClause, null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T, TOrderBy>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		Expression<Func<T, TOrderBy>> orderByAscending,
		int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetQueryEnumerableAsync<T>(
			whereClause,
			m => m.OrderBy(orderByAscending),
			maxRecordsPerOperation,
			cancellationToken
		);

	#endregion GetQueryEnumerableAsync

	#region GetListEnumerableAsync

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetListEnumerableAsync<T>(null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T, TOrderBy>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, TOrderBy>> orderByAscending,
		int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetListEnumerableAsync<T>(
			m => m.OrderBy(orderByAscending),
			maxRecordsPerOperation,
			cancellationToken
		);

	#endregion GetListEnumerableAsync

	#region QueryAsync

	public static Task<ContinuationResponse<T>> QueryAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.QueryAsync<T>(
			whereClause,
			orderByClause,
			new ContinuationRequest { MaxRecords = maxRecordCount },
			cancellationToken
		);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		ContinuationRequest continuationRequest,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.QueryAsync<T>(whereClause, null, continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.QueryAsync<T>(
			whereClause,
			null,
			new ContinuationRequest { MaxRecords = maxRecordCount },
			cancellationToken
		);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		Expression<Func<T, bool>> orderByClause,
		ContinuationRequest continuationRequest,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.QueryAsync<T>(whereClause, m => m.OrderBy(orderByClause), continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		Expression<Func<T, bool>> orderByClause,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.QueryAsync<T>(whereClause, m => m.OrderBy(orderByClause), maxRecordCount, cancellationToken);

	#endregion QueryAsync

	#region ListAsync

	public static Task<ContinuationResponse<T>> ListAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.ListAsync<T>(
			orderByClause,
			new ContinuationRequest { MaxRecords = maxRecordCount },
			cancellationToken
		);

	public static Task<ContinuationResponse<T>> ListAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		ContinuationRequest continuationRequest,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.ListAsync<T>(null, continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.ListAsync<T>(null, new ContinuationRequest { MaxRecords = maxRecordCount }, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> orderByClause,
		ContinuationRequest continuationRequest,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.ListAsync<T>(m => m.OrderBy(orderByClause), continuationRequest, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> orderByClause,
		int maxRecordCount = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.ListAsync<T>(m => m.OrderBy(orderByClause), maxRecordCount, cancellationToken);

	#endregion ListAsync

	#region CountAsync

	public static Task<long> CountAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.CountAsync<T>(null, cancellationToken);

	#endregion CountAsync

	#region FirstOrDefaultAsync

	public static Task<T?> FirstOrDefaultAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.FirstOrDefaultAsync<T>(whereClause, null, cancellationToken);

	public static Task<T?> FirstOrDefaultAsync<T>(
		[NotNull] this IQueryableEventStore eventStore,
		Expression<Func<T, bool>> whereClause,
		Expression<Func<T, bool>> orderByClause,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.FirstOrDefaultAsync<T>(whereClause, m => m.OrderBy(orderByClause), cancellationToken);

	#endregion FirstOrDefaultAsync
}
