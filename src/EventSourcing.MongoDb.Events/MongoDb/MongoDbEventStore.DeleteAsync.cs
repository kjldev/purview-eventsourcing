using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	public async Task<bool> DeleteAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		if (aggregate.Details.IsDeleted)
			throw AggregateIsDeletedException(aggregate.Id());

		operationContext ??= EventStoreOperationContext.DefaultContext;

		if (aggregate.IsNew())
			return false;

		if (operationContext.PermanentlyDelete)
			return await PermanentlyDeleteAsync(aggregate, operationContext, cancellationToken);

		DeleteEvent deleteAggregateEvent = new()
		{
			Details = {
				AggregateVersion = aggregate.Details.CurrentVersion + 1,
				When = DateTimeOffset.UtcNow
			}
		};
		aggregate.ApplyEvent(deleteAggregateEvent);

		var result = await SaveCoreAsync(aggregate, operationContext, cancellationToken, deleteAggregateEvent);

		return result.Saved;
	}

	async Task<bool> PermanentlyDeleteAsync(T aggregate, EventStoreOperationContext operationContext, CancellationToken cancellationToken = default)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		var aggregateId = aggregate.Id();
		var streamVersion = await GetStreamVersionAsync(aggregateId, true, cancellationToken);
		if (streamVersion == null)
			return false;

		_eventStoreTelemetry.PermanentDeleteRequested(aggregateId);
		try
		{
			var ids = Enumerable.Range(1, streamVersion.Version).Select(id => CreateEventId(aggregateId, id).ToString());

			List<string> entitiesToDelete = [.. ids];
			entitiesToDelete.Add(streamVersion.Id);

			if (operationContext.UseIdempotencyMarker)
			{
				var results = _eventClient.QueryEnumerableAsync<IdempotencyMarkerEntity>(m => m.AggregateId == aggregateId && m.EntityType == EntityTypes.IdempotencyMarkerType, cancellationToken: cancellationToken);
				await foreach (var entity in results)
					entitiesToDelete.Add(entity.Id);
			}

			await _eventClient.SubmitDeleteBatchAsync(entitiesToDelete, cancellationToken);
			await _snapshotClient.DeleteAsync<SnapshotEntity>(m => m.Id == aggregateId, cancellationToken);

			_eventStoreTelemetry.PermanentDeleteComplete(aggregateId);

			aggregate.Details.IsDeleted = true;
			aggregate.Details.Locked = true;

			return true;
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.PermanentDeleteFailed(aggregateId, ex);

			return false;
		}
		finally
		{
			ClearCacheFireAndForget(aggregate);
		}
	}
}
