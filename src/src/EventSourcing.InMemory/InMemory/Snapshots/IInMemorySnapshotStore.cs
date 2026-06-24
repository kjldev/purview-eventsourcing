using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.InMemory.Snapshots;

public interface IInMemorySnapshotStore<T> : IQueryableEventStoreCore<T>
	where T : class, IAggregate, new() { }
