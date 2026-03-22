using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

public interface ICosmosDbSnapshotEventStore<T> : IQueryableEventStore<T>
	where T : class, IAggregate, new()
{
	/// <summary>
	/// This will force snapshot the aggregate regardless of it's save state in the internal event store.
	/// </summary>
	/// <param name="aggregate">The aggregate to upsert.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task.</returns>
	Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default);
}
