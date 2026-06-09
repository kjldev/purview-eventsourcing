using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class OldEvent : EventBase
{
	public Guid Value { get; set; }

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Value);
	}
}
