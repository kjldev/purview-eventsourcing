using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
    static IEvent? DeserializeEvent(string eventContent, Type eventType) =>
        EventStoreSerializationHelpers.Deserialize(eventContent, eventType) as IEvent;

    static string SerializeSnapshot(T aggregate) =>
        EventStoreSerializationHelpers.Serialize(aggregate, aggregate.GetType());

    static string SerializeEvent(IEvent @event) =>
        EventStoreSerializationHelpers.Serialize(@event, @event.GetType());

    static T DeserializeSnapshot(string aggregateContent) =>
        EventStoreSerializationHelpers.Deserialize<T>(aggregateContent)!;
}
