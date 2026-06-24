using System.ComponentModel;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing;

/// <summary>
/// Provider-facing typed event-store contract used by concrete implementations and internal infrastructure.
/// </summary>
/// <typeparam name="T">An <see cref="IAggregate"/> implementation.</typeparam>
/// <seealso cref="IQueryableEventStoreCore{T}"/>
/// <seealso cref="IAggregate"/>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventStoreCore<T>
	where T : class, IAggregate, new()
{
	Task<T> CreateAsync(string? aggregateId = null, CancellationToken cancellationToken = default);

	Task<T?> GetOrCreateAsync(
		string? aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	Task<T?> GetAsync(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	Task<T?> GetAtAsync(
		string aggregateId,
		int version,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	Task<SaveResult<T>> SaveAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default);

	Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	Task<bool> RestoreAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	);

	IAsyncEnumerable<string> GetAggregateIdsAsync(bool includeDeleted, CancellationToken cancellationToken = default);

	Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default);

	T FulfilRequirements(T aggregate);

	IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
		string aggregateId,
		int versionFrom,
		int? versionTo,
		CancellationToken cancellationToken
	);
}
