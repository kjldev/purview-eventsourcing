namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	public async Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		var aggregate = await GetCoreAsync(aggregateId, new()
		{
			CacheMode = EventStoreCachingOptions.None,
			DeleteMode = DeleteHandlingMode.ReturnsAggregate
		}, cancellationToken);

		if (aggregate == null)
			return null;

		if (!aggregate.Details.IsDeleted)
			throw AggregateNotDeletedException(aggregateId);

		return FulfilRequirements(aggregate);
	}
}
