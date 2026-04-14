using System.ComponentModel;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IQueryableEventStoreImplementationAccessor : IEventStoreImplementationAccessor
{
	IQueryableEventStoreImpl<T> GetQueryableEventStore<T>()
		where T : class, IAggregate, new();
}
