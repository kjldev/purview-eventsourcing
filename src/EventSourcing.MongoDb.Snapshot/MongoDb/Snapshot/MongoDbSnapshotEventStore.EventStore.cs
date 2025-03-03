using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStore<T>
{
	public Task<T> CreateAsync(string? aggregateId = null, CancellationToken cancellationToken = default)
		=> _eventStore.CreateAsync(aggregateId, cancellationToken);

	public Task<T?> GetOrCreateAsync(string? aggregateId, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
		=> _eventStore.GetOrCreateAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAsync(string aggregateId, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
		=> _eventStore.GetAsync(aggregateId, operationContext, cancellationToken);

	public Task<T?> GetAtAsync(string aggregateId, int version, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
		=> _eventStore.GetAtAsync(aggregateId, version, operationContext, cancellationToken);

	public async Task<SaveResult<T>> SaveAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		var result = await _eventStore.SaveAsync(aggregate, operationContext, cancellationToken);
		if (result)
			await SnapshotAsync(aggregate, cancellationToken);

		return result;
	}

	public Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
		=> _eventStore.IsDeletedAsync(aggregateId, cancellationToken);

	public Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
		=> _eventStore.GetDeletedAsync(aggregateId, cancellationToken);

	public async Task<bool> DeleteAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		var result = await _eventStore.DeleteAsync(aggregate, operationContext, cancellationToken);
		if (result)
			await _mongoDbClient.DeleteAsync(BuildPredicate(aggregate), cancellationToken);

		return result;
	}

	public async Task<bool> RestoreAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		var result = await _eventStore.RestoreAsync(aggregate, operationContext, cancellationToken);
		if (result)
			await _mongoDbClient.UpsertAsync(aggregate, BuildPredicate(aggregate), cancellationToken);

		return result;
	}

	public IAsyncEnumerable<string> GetAggregateIdsAsync(bool includeDeleted, CancellationToken cancellationToken = default)
		=> _eventStore.GetAggregateIdsAsync(includeDeleted, cancellationToken);

	public Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default)
		=> _eventStore.ExistsAsync(aggregateId, cancellationToken);

	public T FulfilRequirements(T aggregate)
		=> _eventStore.FulfilRequirements(aggregate);
}
