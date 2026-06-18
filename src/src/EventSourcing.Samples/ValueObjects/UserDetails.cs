using Purview.EventSourcing.Serialization;
using Purview.EventSourcing.ValueObjects;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public sealed partial record UserDetails(Guid Id, string? DisplayName, bool IsActive = true) : IValueObject<UserDetails>
{
	static partial void OnNormalize(ref Guid id, ref string? displayName, ref bool isActive)
	{
		if (!isActive)
			displayName = null;
	}

	partial void OnValidate(Guid id, string? displayName, bool isActive)
	{
		if (id == Guid.Empty)
			throw new ArgumentException("Id must be a valid GUID.", nameof(id));

		if (isActive && string.IsNullOrWhiteSpace(displayName))
			throw new ArgumentException("DisplayName cannot be null or empty.", nameof(displayName));
	}

	public static UserDetails Empty => new(Guid.Empty, null, false);
}
