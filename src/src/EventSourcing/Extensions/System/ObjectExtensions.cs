using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

[EditorBrowsable(EditorBrowsableState.Never)]
static class ObjectExtensions
{
	extension(string? value)
	{
		/// <summary>
		/// Gets the value of <paramref name="value"/>, unless it's null, empty string or whitespace. Then the
		/// value of <paramref name="default"/> is returned.
		/// </summary>
		/// <param name="default">The default value to return.</param>
		/// <returns>Either <paramref name="value"/>, or <paramref name="default"/> if it's null, empty or whitespace.</returns>
		[return: NotNull]
		public string OrDefault(object @default)
		{
			@default.Guard();

			return string.IsNullOrWhiteSpace(value) ? $"{@default}" : value;
		}

		/// <summary>
		/// Guards a string from being null, empty or whitespace. Throws an <see cref="ArgumentNullException"/> if it is with the
		/// <paramref name="paramName" />.
		/// </summary>
		/// <returns>Returns a non-null, non-empty version of <paramref name="value"/>.</returns>
		/// <param name="paramName">The name of the parameter.</param>
		/// <param name="trimWhitespaceToNull">If true, trims the value to null if it's empty or whitespace.</param>
		[StackTraceHidden]
		public string Guard(
			[CallerArgumentExpression(nameof(value))] string? paramName = null,
			bool trimWhitespaceToNull = true
		)
		{
			if (trimWhitespaceToNull)
				value = string.IsNullOrWhiteSpace(value) ? null : value;

			ArgumentNullException.ThrowIfNull(value, paramName);

			return value;
		}
	}

	extension<T>([NotNull] T? value)
	{
		/// <summary>
		/// Guards an object from being null. Throws an <see cref="ArgumentNullException"/> if it is with the
		/// <paramref name="paramName" />.
		/// </summary>
		/// <returns>Returns a non-null, non-empty version of <paramref name="value"/>.</returns>
		/// <param name="paramName">The name of the parameter.</param>
		[StackTraceHidden]
		[return: NotNull]
		public T Guard([CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			ArgumentNullException.ThrowIfNull(value, paramName);

			return value;
		}
	}
}
