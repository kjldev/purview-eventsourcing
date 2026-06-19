using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public sealed partial record UserCapture(UserDetails User, DateTimeOffset OccurredAt)
{
	public bool IsEssentialChange(UserCapture userDetails) =>
		OccurredAt != userDetails?.OccurredAt || User.Id != userDetails?.User.Id;

	public static UserCapture Empty => new(UserDetails.Empty, DateTimeOffset.MinValue);
}
