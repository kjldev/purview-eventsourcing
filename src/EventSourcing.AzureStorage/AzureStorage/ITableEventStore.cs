using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.AzureStorage;

public interface ITableEventStore<T> : INonQueryableEventStore<T>
	where T : class, IAggregate, new()
{
}
