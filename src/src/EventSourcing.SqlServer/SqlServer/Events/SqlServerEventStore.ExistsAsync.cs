namespace Purview.EventSourcing.SqlServer.Events;

partial class SqlServerEventStore<T>
{
	public async Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		var streamVersion = await GetStreamVersionAsync(aggregateId, false, cancellationToken);
		return streamVersion == null
			? ExistsState.DoesNotExist
			: new ExistsState
			{
				Status = streamVersion.IsDeleted ? ExistsStatus.ExistsInDeletedState : ExistsStatus.Exists,
				Version = streamVersion.Version,
			};
	}
}
