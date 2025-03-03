using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.MongoDB;

public interface IMongoDBEventStore<T> : INonQueryableEventStore<T>
	where T : class, IAggregate, new()
{
}
