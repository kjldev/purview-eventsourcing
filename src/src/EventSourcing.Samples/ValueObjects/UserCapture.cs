using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public sealed partial record UserCaptureRecord(UserDetails User, DateTimeOffset OccurredAt)
{
	public bool IsEssentialChange(UserCaptureRecord userDetails) =>
		OccurredAt != userDetails?.OccurredAt || User.Id != userDetails?.User.Id;
}

[ValueObject]
public partial record struct UserCaptureRecordStruct(UserDetails User, DateTimeOffset OccurredAt);

[ValueObject]
public readonly partial record struct UserCaptureRecordStruct1(UserDetails User, DateTimeOffset OccurredAt);

[ValueObject]
public sealed partial record class UserCaptureRecordClass(UserDetails User, DateTimeOffset OccurredAt);

[ValueObject]
public sealed partial class UserCaptureClass(UserDetails User, DateTimeOffset OccurredAt);

[ValueObject]
public partial struct UserCaptureStruct(UserDetails User, DateTimeOffset OccurredAt);
