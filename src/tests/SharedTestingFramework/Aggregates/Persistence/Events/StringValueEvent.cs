using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class StringValueEvent : EventBase
{
	public string Value { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Value);
	}
}
