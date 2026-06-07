using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class QueryableEventStoreFacade(IServiceProvider serviceProvider)
    : IQueryableEventStore,
        IQueryableEventStoreImplementationAccessor
{
    readonly ConcurrentDictionary<Type, object> _queryableEventStores = new();

    public Task<T> CreateAsync<T>(
        string? aggregateId = null,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().CreateAsync(aggregateId, cancellationToken);

    public Task<T?> GetOrCreateAsync<T>(
        string? aggregateId,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .GetOrCreateAsync(aggregateId, operationContext, cancellationToken);

    public Task<T?> GetAsync<T>(
        string aggregateId,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().GetAsync(aggregateId, operationContext, cancellationToken);

    public Task<T?> GetAtAsync<T>(
        string aggregateId,
        int version,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .GetAtAsync(aggregateId, version, operationContext, cancellationToken);

    public Task<SaveResult<T>> SaveAsync<T>(
        T aggregate,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().SaveAsync(aggregate, operationContext, cancellationToken);

    public Task<bool> IsDeletedAsync<T>(
        string aggregateId,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().IsDeletedAsync(aggregateId, cancellationToken);

    public Task<T?> GetDeletedAsync<T>(
        string aggregateId,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().GetDeletedAsync(aggregateId, cancellationToken);

    public Task<bool> DeleteAsync<T>(
        T aggregate,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().DeleteAsync(aggregate, operationContext, cancellationToken);

    public Task<bool> RestoreAsync<T>(
        T aggregate,
        EventStoreOperationContext? operationContext,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().RestoreAsync(aggregate, operationContext, cancellationToken);

    public IAsyncEnumerable<string> GetAggregateIdsAsync<T>(
        bool includeDeleted,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().GetAggregateIdsAsync(includeDeleted, cancellationToken);

    public Task<ExistsState> ExistsAsync<T>(
        string aggregateId,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().ExistsAsync(aggregateId, cancellationToken);

    public T FulfilRequirements<T>(T aggregate)
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().FulfilRequirements(aggregate);

    public IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .GetQueryEnumerableAsync(
                whereClause,
                orderByClause,
                maxRecordsPerIteration,
                cancellationToken
            );

    public IAsyncEnumerable<T> GetListEnumerableAsync<T>(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .GetListEnumerableAsync(orderByClause, maxRecordsPerIteration, cancellationToken);

    public Task<ContinuationResponse<T>> QueryAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .QueryAsync(whereClause, orderByClause, request, cancellationToken);

    public Task<ContinuationResponse<T>> ListAsync<T>(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().ListAsync(orderByClause, request, cancellationToken);

    public Task<long> CountAsync<T>(
        Expression<Func<T, bool>>? whereClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().CountAsync(whereClause, cancellationToken);

    public Task<T?> SingleOrDefaultAsync<T>(
        Expression<Func<T, bool>> whereClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>().SingleOrDefaultAsync(whereClause, cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        CancellationToken cancellationToken = default
    )
        where T : class, IAggregate, new() =>
        GetQueryableEventStore<T>()
            .FirstOrDefaultAsync(whereClause, orderByClause, cancellationToken);

    public IEventStoreCore<T> GetEventStore<T>()
        where T : class, IAggregate, new() => GetQueryableEventStore<T>();

    public IQueryableEventStoreCore<T> GetQueryableEventStore<T>()
        where T : class, IAggregate, new() =>
        (IQueryableEventStoreCore<T>)
            _queryableEventStores.GetOrAdd(
                typeof(T),
                _ => serviceProvider.GetRequiredService<IQueryableEventStoreCore<T>>()
            );
}
