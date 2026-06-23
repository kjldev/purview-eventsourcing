using System.Diagnostics.CodeAnalysis;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing;

[System.Diagnostics.DebuggerStepThrough]
[SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public static class IEventStoreHistoryExtensions
{
	extension([NotNull] IEventStore eventStore)
	{
		public Task<ContinuationResponse<AggregateEventHistoryItem>> GetEventHistoryAsync<T>(
			string aggregateId,
			AggregateEventHistoryRequest? request = null,
			CancellationToken cancellationToken = default
		)
			where T : class, IAggregate, new()
		{
			ArgumentNullException.ThrowIfNull(eventStore);

			var typedStore = (eventStore as IEventStoreImplementationAccessor)?.GetEventStore<T>();
			return typedStore == null
				? throw new NotSupportedException(
					$"The configured event store '{eventStore.GetType().FullName}' does not expose implementation accessors required for event history."
				)
				: typedStore.GetEventHistoryAsync(aggregateId, request, cancellationToken);
		}
	}
}
