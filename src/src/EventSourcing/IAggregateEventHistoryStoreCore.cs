using System.ComponentModel;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing;

/// <summary>
/// Provider-facing contract for stores that can enumerate aggregate events by version range.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAggregateEventHistoryStoreCore<T>
	where T : class, IAggregate, new()
{
	IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
		string aggregateId,
		int versionFrom,
		int? versionTo,
		CancellationToken cancellationToken
	);
}
