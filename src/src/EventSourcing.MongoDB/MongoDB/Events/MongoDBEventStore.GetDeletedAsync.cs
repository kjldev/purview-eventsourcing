namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	public async Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		var aggregate = await GetCoreAsync(
			aggregateId,
			new() { SnapshotCacheMode = SnapshotCachingOptions.None, DeleteMode = DeleteHandlingMode.ReturnsAggregate },
			cancellationToken
		);

		return aggregate == null ? null
			: aggregate.Details.IsDeleted ? FulfilRequirements(aggregate)
			: throw AggregateNotDeletedException(aggregateId);
	}
}
