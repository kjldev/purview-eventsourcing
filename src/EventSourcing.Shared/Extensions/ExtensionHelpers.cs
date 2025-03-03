using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
static class ExtensionHelpers
{
	[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	public static string ToLowerSafe(this string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

		return value.ToLowerInvariant();
	}

	/// <summary>
	/// Gets the value of <paramref name="value"/>, unless it's null, empty string or whitespace. Then the
	/// value of <paramref name="default"/> is returned.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="default">The default value to return.</param>
	/// <returns>Either <paramref name="value"/>, or <paramref name="default"/> if it's null, empty or whitespace.</returns>
	[return: NotNull]
	public static string OrDefault(this string? value, object @default)
	{
		@default.Guard();

		return string.IsNullOrWhiteSpace(value)
			? $"{@default}"
			: value;
	}

	/// <summary>
	/// Guards an object from being null. Throws an <see cref="ArgumentNullException"/> if it is with the
	/// <paramref name="paramName" />.
	/// </summary>
	/// <returns>Returns a non-null, non-empty version of <paramref name="value"/>.</returns>
	/// <param name="value">The value to guard.</param>
	/// <param name="paramName">The name of the parameter.</param>
	[StackTraceHidden]
	[return: NotNull]
	public static T Guard<T>([NotNull] this T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
	{
		ArgumentNullException.ThrowIfNull(value, paramName);

		return value;
	}

	/// <summary>
	/// Guards a string from being null, empty or whitespace. Throws an <see cref="ArgumentNullException"/> if it is with the
	/// <paramref name="paramName" />.
	/// </summary>
	/// <returns>Returns a non-null, non-empty version of <paramref name="value"/>.</returns>
	/// <param name="value">The value to guard.</param>
	/// <param name="paramName">The name of the parameter.</param>
	/// <param name="trimWhitespaceToNull">If true, trims the value to null if it's empty or whitespace.</param>
	[StackTraceHidden]
	public static string Guard(this string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool trimWhitespaceToNull = true)
	{
		if (trimWhitespaceToNull)
			value = value.OrNull();

		ArgumentNullException.ThrowIfNull(value, paramName);

		return value;
	}

	/// <summary>
	/// <para>If <paramref name="item"/> has a value other than null, empty string or whitespace, returns it.</para>
	/// <para>Otherwise returns null.</para>
	/// </summary>
	/// <param name="item">The item to check.</param>
	/// <returns>The string value, or null.</returns>
	public static string? OrNull(this string? item)
		=> string.IsNullOrWhiteSpace(item)
			? null
			: item;
}
