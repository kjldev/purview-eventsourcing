using System.ComponentModel;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IEventStoreImplementationAccessor
{
    IEventStoreCore<T> GetEventStore<T>()
        where T : class, IAggregate, new();
}
