using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Services;

public interface IAggregateEventNameMapper
{
	string GetName<T>(IEvent aggregateEvent)
		where T : IAggregate;

	string GetName<T>(Type aggregateEventType)
		where T : IAggregate;

	string? GetTypeName<T>(string eventTypeName)
		where T : IAggregate;

	string InitializeAggregate<T>()
		where T : class, IAggregate, new();
}
