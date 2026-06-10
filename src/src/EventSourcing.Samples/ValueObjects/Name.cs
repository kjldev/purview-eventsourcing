using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[Scalar]
public readonly partial record struct Name
{
	public string Value { get; }

	Name(string value) => Value = value;

	public static Name Create(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("Name cannot be empty.", nameof(value));

		return new(value);
	}

	public static implicit operator string(Name name) => name.Value;

	public static implicit operator Name(string value) => Create(value);
}
