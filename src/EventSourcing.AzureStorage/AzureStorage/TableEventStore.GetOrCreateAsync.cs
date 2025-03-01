namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	public async Task<T?> GetOrCreateAsync(string? aggregateId, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(aggregateId))
		{
			var exists = await ExistsAsync(aggregateId, cancellationToken);
			if (exists)
				return await GetAsync(aggregateId, operationContext, cancellationToken);

			return null;
		}

		return await CreateAsync(aggregateId, cancellationToken);
	}
}
