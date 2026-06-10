using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[Scalar]
public readonly partial record struct Name : IComparable<string>, IComparable
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

	public int CompareTo(string? other)
	{
		return Value.CompareTo(other);
	}

	public int CompareTo(object? obj)
	{
		if (obj is null)
			return 1;
		if (obj is Name otherName)
			return CompareTo(otherName.Value);
		if (obj is string otherString)
			return CompareTo(otherString);
		throw new ArgumentException($"Object must be of type {nameof(Name)} or string.", nameof(obj));
	}
}
