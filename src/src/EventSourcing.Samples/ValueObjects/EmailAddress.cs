using System.Text.RegularExpressions;
using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[Scalar]
public readonly partial record struct EmailAddress : IComparable<string>, IComparable
{
	public string Value { get; }

	EmailAddress(string value) => Value = value;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static EmailAddress Create(string value)
	{
		value = value?.Trim().ToLowerInvariant()!;

		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("Email address cannot be empty.", nameof(value));
		if (!EmailRegex().IsMatch(value))
			throw new ArgumentException("Invalid email address format.", nameof(value));

		return new(value);
	}

	public static implicit operator string(EmailAddress email) => email.Value;

	public static implicit operator EmailAddress(string value) => Create(value);

	[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
	private static partial Regex EmailRegex();

	public int CompareTo(string? other)
	{
		return Value.CompareTo(other);
	}

	public int CompareTo(object? obj)
	{
		if (obj is null)
			return 1;
		if (obj is EmailAddress otherEmail)
			return CompareTo(otherEmail.Value);
		if (obj is string otherString)
			return CompareTo(otherString);
		throw new ArgumentException($"Object must be of type {nameof(EmailAddress)} or string.", nameof(obj));
	}
}
