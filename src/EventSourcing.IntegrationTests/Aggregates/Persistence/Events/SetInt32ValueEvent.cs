using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class SetInt32ValueEvent : EventBase
{
	public int Value { get; set; }

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Value);
	}
}
