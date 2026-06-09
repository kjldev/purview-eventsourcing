using Purview.EventSourcing.SqlServer.Events.Exceptions;

namespace Purview.EventSourcing.SqlServer.Events;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter")]
partial class SqlServerEventStore<T>
{
	static ArgumentNullException NullAggregate(T? aggregate) => new(nameof(aggregate));

	static AggregateIsDeletedException AggregateIsDeletedException(string aggregateId) => new(aggregateId);

	static AggregateNotDeletedException AggregateNotDeletedException(string aggregateId) => new(aggregateId);
}
