using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	static IEvent? DeserializeEvent(string eventContent, Type eventType) =>
		JsonHelpers.Deserialize(eventContent, eventType) as IEvent;

	static string SerializeSnapshot(T aggregate) =>
		JsonHelpers.Serialize(aggregate, aggregate.GetType());

	static string SerializeEvent(IEvent @event) =>
		JsonHelpers.Serialize(@event, @event.GetType());

	static T DeserializeSnapshot(string aggregateContent) =>
		JsonHelpers.Deserialize<T>(aggregateContent)!;
}
