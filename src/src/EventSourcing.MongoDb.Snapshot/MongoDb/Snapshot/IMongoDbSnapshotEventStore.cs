using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB.Snapshot;

public interface IMongoDBSnapshotEventStore<T> : IQueryableEventStoreCore<T>
    where T : class, IAggregate, new()
{
    Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default);
}
