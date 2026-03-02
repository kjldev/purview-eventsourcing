using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.SqlServer;

public interface ISqlServerEventStore<T> : INonQueryableEventStore<T>
	where T : class, IAggregate, new() { }
