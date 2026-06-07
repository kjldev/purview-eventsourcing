using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

/// <summary>
/// Provides querying and sorting operations to an <see cref="IEventStore"/>.
/// </summary>
public interface IQueryableEventStore : IEventStore
{
    IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    IAsyncEnumerable<T> GetListEnumerableAsync<T>(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    Task<ContinuationResponse<T>> QueryAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    Task<ContinuationResponse<T>> ListAsync<T>(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    Task<long> CountAsync<T>(
        Expression<Func<T, bool>>? whereClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    Task<T?> SingleOrDefaultAsync<T>(
        Expression<Func<T, bool>> whereClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();

    Task<T?> FirstOrDefaultAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new();
}
