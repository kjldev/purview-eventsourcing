using Microsoft.Extensions.Primitives;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class AddStringValuesDictionaryKVPsEvent : EventBase
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public KeyValuePair<string, StringValues>[] KVPs { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		foreach (var kvp in KVPs)
		{
			hash.Add(kvp.Key);
			hash.Add(kvp.Value);
		}
	}
}
