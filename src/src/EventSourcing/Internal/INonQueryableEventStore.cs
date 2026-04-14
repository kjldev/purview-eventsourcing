using System.ComponentModel;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Internal;

[EditorBrowsable(EditorBrowsableState.Never)]
public interface INonQueryableEventStore<T> : IEventStoreImpl<T>
	where T : class, IAggregate, new() { }
