using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Aggregates.Test.Events;

public class AppendToReadOnlyDictionaryEvent : EventBase
{
	public string Key { get; set; } = default!;

	public IEnumerable<string> Values { get; set; } = [];

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Key);
		if (Values is not null)
		{
			foreach (string value in Values)
				hash.Add(value);
		}
	}
}
