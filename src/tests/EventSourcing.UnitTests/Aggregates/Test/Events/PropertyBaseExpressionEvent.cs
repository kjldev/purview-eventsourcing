using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Test.Events;

public class PropertyBaseExpressionEvent : EventBase
{
	public string? PropertyValue { get; set; }

	public string PropertyName { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(PropertyValue);
		hash.Add(PropertyName);
	}
}
