using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class IncrementInt32ValueEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
