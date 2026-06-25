using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.ValueObjects;

[GenerateAggregate]
sealed partial class ValueObjectTestAggregate
{
	public UserCapture UserCapture { get; private set; }

	[GenerateAggregateEvent]
	public partial void SetUserCapture(UserCapture userCapture);
}
