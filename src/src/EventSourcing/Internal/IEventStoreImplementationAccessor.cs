using System.ComponentModel;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventStoreImplementationAccessor
{
	IEventStoreImpl<T> GetEventStore<T>()
		where T : class, IAggregate, new();
}
