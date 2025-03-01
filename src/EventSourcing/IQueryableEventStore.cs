using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Provides querying and sorting operations to a <see cref="IEventStore{T}"/>.
/// </summary>
/// <typeparam name="T">An <see cref="IAggregate"/> implementation.</typeparam>
public interface IQueryableEventStore<T> : IEventStore<T>
	where T : class, IAggregate, new()
{
	/// <summary>
	/// Queries aggregates in the event store as an enumerable stream, given a <paramref name="whereClause">where expression</paramref>.
	/// </summary>
	/// <param name="whereClause">The <see cref="Expression{TDelegate}"/> predicate used for filtering.</param>
	/// <param name="orderByClause">Optional, the <see cref="Func{T, TResult}"/> used for ordering operations.</param>
	/// <param name="maxRecordsPerIteration">The maximum number of records to return per-iterantion.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A paged result set of <typeparamref name="T"/>.</returns>
	/// <remarks>The <paramref name="whereClause"/> and <paramref name="orderByClause"/> are limited by the underlying LINQ-based implementation.</remarks>
	IAsyncEnumerable<T> GetQueryEnumerableAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists aggregates in the event store as an enumerable stream.
	/// </summary>
	/// <param name="orderByClause">Optional, the <see cref="Func{T, TResult}"/> used for ordering operations.</param>
	/// <param name="maxRecordsPerIteration">The maximum number of records to return per-iterantion.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A paged result set of <typeparamref name="T"/>.</returns>
	/// <remarks>The <paramref name="orderByClause"/> is limited by the underlying LINQ-based implementation.</remarks>
	/// <seealso cref="IAsyncEnumerable{T}"/>
	IAsyncEnumerable<T> GetListEnumerableAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default);

	/// <summary>
	/// Queries aggregates in the event store given a <paramref name="whereClause">where expression</paramref>.
	/// </summary>
	/// <param name="whereClause">The <see cref="Expression{TDelegate}"/> predicate used for filtering.</param>
	/// <param name="orderByClause">Optional, the <see cref="Func{T, TResult}"/> used for ordering operations.</param>
	/// <param name="request">A <see cref="ContinuationRequest"/> that defines things like the <see cref="ContinuationRequest.MaxRecords"/>.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A paged result set of <typeparamref name="T"/>.</returns>
	/// <remarks>The <paramref name="whereClause"/> and <paramref name="orderByClause"/> are limited by the underlying LINQ-based implementation.</remarks>
	Task<ContinuationResponse<T>> QueryAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists aggregates in the event store.
	/// </summary>
	/// <param name="orderByClause">Optional, the <see cref="Func{T, TResult}"/> used for ordering operations.</param>
	/// <param name="request">A <see cref="ContinuationRequest"/> that defines things like the <see cref="ContinuationRequest.MaxRecords"/>.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A paged result set of <typeparamref name="T"/>.</returns>
	/// <remarks>The <paramref name="orderByClause"/> is limited by the underlying LINQ-based implementation.</remarks>
	Task<ContinuationResponse<T>> ListAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Counts the aggregates in the event store given a <paramref name="whereClause">where expression</paramref>.
	/// </summary>
	/// <param name="whereClause">The <see cref="Expression{TDelegate}"/> predicate used for filtering.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A count of the aggregates matching the <paramref name="whereClause"/>.</returns>
	/// <remarks>The result and the <paramref name="whereClause"/> are limited by the underlying LINQ-based implementation.</remarks>
	Task<long> CountAsync(Expression<Func<T, bool>>? whereClause, CancellationToken cancellationToken = default);

	/// <summary>
	/// Queries for a single aggregate in the event store given a <paramref name="whereClause">where expression</paramref>.
	/// </summary>
	/// <param name="whereClause">The <see cref="Expression{TDelegate}"/> predicate used for filtering.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>A single aggregate, or null.</returns>
	/// <remarks>The <paramref name="whereClause"/> are limited by the underlying LINQ-based implementation.</remarks>
	Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> whereClause, CancellationToken cancellationToken = default);

	/// <summary>
	/// Queries for the first aggregate in the event store given a <paramref name="whereClause">where expression</paramref> and,
	/// optionally, a <paramref name="orderByClause"/>.
	/// </summary>
	/// <param name="whereClause">The <see cref="Expression{TDelegate}"/> predicate used for filtering.</param>
	/// <param name="orderByClause">Optional, the <see cref="Func{T, TResult}"/> used for ordering operations.</param>
	/// <param name="cancellationToken">A stopping token.</param>
	/// <returns>The first aggregate found, or null.</returns>
	/// <remarks>The <paramref name="whereClause"/> and <paramref name="orderByClause"/> are limited by the underlying LINQ-based implementation.</remarks>
	Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, CancellationToken cancellationToken = default);
}
