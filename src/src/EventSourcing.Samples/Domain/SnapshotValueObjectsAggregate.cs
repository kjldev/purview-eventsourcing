using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

[GenerateAggregate]
public sealed partial class SnapshotValueObjectsAggregate : AggregateBase
{
	public UserDetails UserDetails { get; private set; } = UserDetails.Hydrate(Guid.Empty, string.Empty);

	public UserDetails2 UserDetails2 { get; private set; } = UserDetails2.Hydrate(Guid.Empty, string.Empty);

	[GenerateAggregateEvent(EventName = "UserDetailsCaptured")]
	public partial SnapshotValueObjectsAggregate CaptureUserDetails(
		UserDetails userDetails,
		UserDetails2 userDetails2
	);
}
