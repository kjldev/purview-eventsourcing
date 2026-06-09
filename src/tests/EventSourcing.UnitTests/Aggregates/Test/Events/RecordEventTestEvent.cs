using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Test.Events;

public class RecordEventTestEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
