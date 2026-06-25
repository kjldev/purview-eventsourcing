using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public sealed partial record UserDetails(Guid Id, string? DisplayName, bool IsActive = true)
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
}
