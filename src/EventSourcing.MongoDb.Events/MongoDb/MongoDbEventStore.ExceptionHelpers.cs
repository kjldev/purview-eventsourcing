using Purview.EventSourcing.MongoDB.Exceptions;

namespace Purview.EventSourcing.MongoDB;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter")]
partial class MongoDBEventStore<T>
{
	static ArgumentNullException NullAggregate(T? aggregate)
		=> new(nameof(aggregate));

	static AggregateIsDeletedException AggregateIsDeletedException(string aggregateId)
		=> new(aggregateId);

	static AggregateNotDeletedException AggregateNotDeletedException(string aggregateId)
		=> new(aggregateId);
}
