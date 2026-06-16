using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[Scalar]
public readonly partial record struct Name
{
	public string Value { get; }

	Name(string value) => Value = value;

	static partial void OnValidate(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException($"{nameof(Name)} cannot be empty.", nameof(value));
	}

	static partial void OnNormalize(ref string value) => value = value?.Trim()!;
}
