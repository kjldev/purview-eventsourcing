using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.ValueObjects;

[GenerateAggregate]
sealed partial class ValueObjectTestAggregate
{
	public UserCaptureRecord UserCaptureRecord { get; private set; }

	[GenerateAggregateEvent]
	public partial void SetUserCapture(UserCaptureRecord userCaptureRecord);
}
