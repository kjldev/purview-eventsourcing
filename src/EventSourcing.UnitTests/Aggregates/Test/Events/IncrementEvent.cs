using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Test.Events;

public class IncrementEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
