namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	public async Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		var streamVersion = await GetStreamVersionAsync(aggregateId, true, cancellationToken);
		return streamVersion == null
			? throw new NullReferenceException($"The aggregate specified ({aggregateId}) does not exist.")
			: streamVersion.IsDeleted;
	}
}
