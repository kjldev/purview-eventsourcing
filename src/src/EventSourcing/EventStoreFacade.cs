using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventStoreFacade(IServiceProvider serviceProvider) : IEventStore, IEventStoreImplementationAccessor
{
	readonly ConcurrentDictionary<Type, object> _eventStores = new();

	public Task<T> CreateAsync<T>(string? aggregateId = null, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new() => GetEventStore<T>().CreateAsync(aggregateId, cancellationToken);

	public Task<T?> GetOrCreateAsync<T>(
		string? aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().GetOrCreateAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAsync<T>(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().GetAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAtAsync<T>(
		string aggregateId,
		int version,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().GetAtAsync(aggregateId, version, operationContext, cancellationToken);

	public Task<SaveResult<T>> SaveAsync<T>(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().SaveAsync(aggregate, operationContext, cancellationToken);

	public Task<bool> IsDeletedAsync<T>(string aggregateId, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new() => GetEventStore<T>().IsDeletedAsync(aggregateId, cancellationToken);

	public Task<T?> GetDeletedAsync<T>(string aggregateId, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new() => GetEventStore<T>().GetDeletedAsync(aggregateId, cancellationToken);

	public Task<bool> DeleteAsync<T>(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().DeleteAsync(aggregate, operationContext, cancellationToken);

	public Task<bool> RestoreAsync<T>(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().RestoreAsync(aggregate, operationContext, cancellationToken);

	public IAsyncEnumerable<string> GetAggregateIdsAsync<T>(
		bool includeDeleted,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		GetEventStore<T>().GetAggregateIdsAsync(includeDeleted, cancellationToken);

	public Task<ExistsState> ExistsAsync<T>(string aggregateId, CancellationToken cancellationToken = default)
		where T : class, IAggregate, new() => GetEventStore<T>().ExistsAsync(aggregateId, cancellationToken);

	public T FulfilRequirements<T>(T aggregate)
		where T : class, IAggregate, new() => GetEventStore<T>().FulfilRequirements(aggregate);

	public IEventStoreCore<T> GetEventStore<T>()
		where T : class, IAggregate, new() =>
		(IEventStoreCore<T>)
			_eventStores.GetOrAdd(typeof(T), _ => serviceProvider.GetRequiredService<IEventStoreCore<T>>());
}
