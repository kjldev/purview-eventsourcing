using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.SqlServer.Events;

public interface ISqlServerEventStore<T> : INonQueryableEventStore<T>, IAggregateEventHistoryStoreCore<T>
	where T : class, IAggregate, new() { }
