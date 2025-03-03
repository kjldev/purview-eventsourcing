namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	public async Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		var streamVersion = await GetStreamVersionAsync(aggregateId, false, cancellationToken);
		if (streamVersion == null)
			return ExistsState.DoesNotExists;

		return new ExistsState
		{
			Status = streamVersion.IsDeleted
				? ExistsStatus.ExistsInDeletedState
				: ExistsStatus.Exists,
			Version = streamVersion.Version
		};
	}
}

