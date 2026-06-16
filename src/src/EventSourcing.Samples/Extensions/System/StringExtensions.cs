using System.ComponentModel;

namespace System;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class StringExtensions
{
	extension(string? value)
	{
		public string? OrNull() => string.IsNullOrWhiteSpace(value) ? null : value;

		public string OrDefault() => value?.Trim() ?? string.Empty;
	}
}
