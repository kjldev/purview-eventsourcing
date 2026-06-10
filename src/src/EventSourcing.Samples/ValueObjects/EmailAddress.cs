using System.Text.RegularExpressions;

namespace Purview.EventSourcing.Samples.ValueObjects;

public readonly partial record struct EmailAddress
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
}
