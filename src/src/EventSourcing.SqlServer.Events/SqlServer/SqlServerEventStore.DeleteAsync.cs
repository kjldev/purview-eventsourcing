using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.SqlServer;

partial class SqlServerEventStore<T>
{
	public async Task<bool> DeleteAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
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
			Details = { AggregateVersion = aggregate.Details.CurrentVersion + 1, When = DateTimeOffset.UtcNow },
		};
		aggregate.ApplyEvent(deleteAggregateEvent);

		var result = await SaveCoreAsync(aggregate, operationContext, null, null, cancellationToken, deleteAggregateEvent);
		await result.AfterCommitAsync(cancellationToken);

		return result.Result.Saved;
	}

	async Task<bool> PermanentlyDeleteAsync(
		T aggregate,
		EventStoreOperationContext operationContext,
		CancellationToken cancellationToken = default
	)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		var aggregateId = aggregate.Id();
		var streamVersion = await GetStreamVersionAsync(aggregateId, true, cancellationToken);
		if (streamVersion == null)
			return false;

		_eventStoreTelemetry.PermanentDeleteRequested(aggregateId);
		using var activity = _eventStoreTelemetry.DeleteAggregate(aggregateId, _aggregateTypeFullName);
		try
		{
			await _client.DeleteByAggregateIdAsync(aggregateId, cancellationToken);

			_eventStoreTelemetry.PermanentDeleteComplete(aggregateId);
			_eventStoreTelemetry.AggregateDeletedCounter(aggregate.AggregateType);

			aggregate.Details.IsDeleted = true;
			aggregate.Details.Locked = true;

			return true;
		}
		#pragma warning disable CA1031
		catch (Exception ex)
		#pragma warning restore CA1031
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
