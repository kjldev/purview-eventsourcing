namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	public async Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		var aggregate = await GetCoreAsync(
			aggregateId,
			new() { CacheMode = EventStoreCachingOptions.None, DeleteMode = DeleteHandlingMode.ReturnsAggregate },
			cancellationToken
		);

		return aggregate == null
			? null
			: aggregate.Details.IsDeleted ? FulfilRequirements(aggregate) : throw AggregateNotDeletedException(aggregateId);
	}
}
