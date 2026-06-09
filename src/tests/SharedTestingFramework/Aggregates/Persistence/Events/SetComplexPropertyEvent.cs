using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class SetComplexPropertyEvent : EventBase
{
	public ComplexTestType ComplexProperty { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(ComplexProperty);
	}
}
