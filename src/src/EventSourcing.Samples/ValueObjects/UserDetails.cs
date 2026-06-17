using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public sealed partial record UserDetails
{
	public Guid Id { get; }

	public string DisplayName { get; }

	partial void OnValidate(Guid id, string displayName)
	{
		if (id == Guid.Empty)
			throw new ArgumentException("Id must be a valid GUID.", nameof(id));

		if (string.IsNullOrWhiteSpace(displayName))
			throw new ArgumentException("DisplayName cannot be null or empty.", nameof(displayName));
	}
}
