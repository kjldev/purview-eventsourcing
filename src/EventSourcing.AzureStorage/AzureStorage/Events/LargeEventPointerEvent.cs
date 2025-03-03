using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.AzureStorage.Events;

public sealed class LargeEventPointerEvent : EventBase
{
	public string SerializedEventType { get; set; } = default!;

	///<inheritdoc />
	protected override void BuildEventHash(ref HashCode hash)
		=> hash.Add(SerializedEventType);
}
