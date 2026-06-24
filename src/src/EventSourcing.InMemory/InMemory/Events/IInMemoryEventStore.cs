using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.InMemory.Events;

public interface IInMemoryEventStore<T> : INonQueryableEventStore<T>, IAggregateEventHistoryStoreCore<T>
	where T : class, IAggregate, new()
{
	Task ClearAsync(CancellationToken cancellationToken = default);
}
