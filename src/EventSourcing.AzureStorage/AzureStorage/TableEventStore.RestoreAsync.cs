using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	public async Task<bool> RestoreAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		if (!aggregate.Details.IsDeleted)
			throw AggregateNotDeletedException(aggregate.Id());

		operationContext ??= EventStoreOperationContext.DefaultContext;

		var restoreAggregateEvent = new RestoreEvent
		{
			Details =
			{
				AggregateVersion = aggregate.Details.CurrentVersion + 1,
				When = DateTimeOffset.UtcNow
			}
		};
		aggregate.ApplyEvent(restoreAggregateEvent);

		if (aggregate.IsNew())
			return false;

		var result = await SaveCoreAsync(aggregate, operationContext, cancellationToken, restoreAggregateEvent);
		return result.Saved;
	}
}
