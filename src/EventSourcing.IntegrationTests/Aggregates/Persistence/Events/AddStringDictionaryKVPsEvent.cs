using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Persistence.Events;

public class AddStringDictionaryKVPsEvent : EventBase
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
	public KeyValuePair<string, string>[] KVPs { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		foreach (var kvp in KVPs)
		{
			hash.Add(kvp.Key);
			hash.Add(kvp.Value);
		}
	}
}
