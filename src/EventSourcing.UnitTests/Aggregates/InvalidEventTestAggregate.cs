using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates;

public class InvalidEventTestAggregate : AggregateBase
{
	protected override void RegisterEvents()
	{
		Register<InvalidEventType>(Apply);
	}

	void Apply(InvalidEventType obj)
	{
	}
}

public class InvalidEventType : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
