namespace Purview.EventSourcing.Samples;

public static class AzureStorageConnectionStringComposer
{
	static readonly HashSet<string> AllowedKeys =
	[
		"AccountKey",
		"AccountName",
		"BlobEndpoint",
		"DefaultEndpointsProtocol",
		"DevelopmentStorageProxyUri",
		"EndpointSuffix",
		"FileEndpoint",
		"QueueEndpoint",
		"SharedAccessSignature",
		"TableEndpoint",
		"UseDevelopmentStorage",
	];

	public static string Normalize(string? value)
	{
		var normalized = NormalizeConnectionString(value);
		if (string.IsNullOrWhiteSpace(normalized))
			return normalized;

		if (normalized.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
			return "UseDevelopmentStorage=true";

		var parts = ParseConnectionStringParts(normalized);
		if (parts.Count == 0)
			return string.Empty;

		foreach (var key in parts.Keys.Except(AllowedKeys, StringComparer.OrdinalIgnoreCase).ToArray())
			parts.Remove(key);

		if (
			parts.TryGetValue("BlobEndpoint", out var blobEndpointRaw)
			&& parts.TryGetValue("AccountName", out var accountName)
			&& Uri.TryCreate(blobEndpointRaw, UriKind.Absolute, out var blobEndpoint)
		)
		{
			var basePath = $"/{accountName}";
			var trimmedPath = blobEndpoint.AbsolutePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
				? basePath
				: blobEndpoint.AbsolutePath;

			UriBuilder builder = new(blobEndpoint) { Path = trimmedPath };
			parts["BlobEndpoint"] = builder.Uri.ToString().TrimEnd('/');
		}

		return string.Join(';', parts.Select(part => $"{part.Key}={part.Value}"));
	}

	public static string BuildEventStoreConnectionString(
		string? eventStoreConnectionString,
		string? blobConnectionString,
		string? fallbackConnectionString
	)
	{
		var eventStore = ParseConnectionStringParts(Normalize(eventStoreConnectionString));
		var blob = ParseConnectionStringParts(Normalize(blobConnectionString));
		var fallback = ParseConnectionStringParts(Normalize(fallbackConnectionString));

		if (
			ContainsUseDevelopmentStorage(eventStore)
			|| ContainsUseDevelopmentStorage(blob)
			|| ContainsUseDevelopmentStorage(fallback)
		)
		{
			return "UseDevelopmentStorage=true";
		}

		Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);
		Merge(fallback, merged);
		Merge(blob, merged);
		Merge(eventStore, merged);

		if (
			!merged.ContainsKey("BlobEndpoint")
			&& merged.TryGetValue("TableEndpoint", out var tableEndpointRaw)
			&& Uri.TryCreate(tableEndpointRaw, UriKind.Absolute, out var tableEndpoint)
		)
		{
			UriBuilder blobBuilder = new(tableEndpoint);
			if (blobBuilder.Port == 10002)
				blobBuilder.Port = 10000;

			merged["BlobEndpoint"] = blobBuilder.Uri.ToString().TrimEnd('/');
		}

		if (
			!merged.ContainsKey("TableEndpoint")
			&& merged.TryGetValue("BlobEndpoint", out var blobEndpointRaw)
			&& Uri.TryCreate(blobEndpointRaw, UriKind.Absolute, out var blobEndpoint)
		)
		{
			UriBuilder tableBuilder = new(blobEndpoint);
			if (tableBuilder.Port == 10000)
				tableBuilder.Port = 10002;

			merged["TableEndpoint"] = tableBuilder.Uri.ToString().TrimEnd('/');
		}

		return string.Join(';', merged.Select(part => $"{part.Key}={part.Value}"));
	}

	public static Dictionary<string, string> ParseConnectionStringParts(string? connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			return [with(StringComparer.OrdinalIgnoreCase)];

		Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
		foreach (
			var part in connectionString.Split(
				';',
				StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
			)
		)
		{
			var split = part.Split('=', 2);
			if (split.Length == 2)
				values[split[0]] = split[1];
		}

		return values;
	}

	static string NormalizeConnectionString(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var parts = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

		return parts.Length == 0 ? string.Empty : string.Join(';', parts);
	}

	static bool ContainsUseDevelopmentStorage(Dictionary<string, string> values) =>
		values.TryGetValue("UseDevelopmentStorage", out var value)
		&& string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

	static void Merge(Dictionary<string, string> source, Dictionary<string, string> target)
	{
		foreach (var (key, value) in source)
		{
			if (!string.IsNullOrWhiteSpace(value))
				target[key] = value;
		}
	}
}
