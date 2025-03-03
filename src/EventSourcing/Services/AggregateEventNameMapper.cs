using System.Collections.Concurrent;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Services;

sealed partial class AggregateEventNameMapper : IAggregateEventNameMapper
{
	readonly ConcurrentDictionary<string, string> _eventNamesByAssemblyTypeName = new(StringComparer.InvariantCulture);
	readonly ConcurrentDictionary<string, string> _eventNamesByDefinedTypeName = new(StringComparer.InvariantCulture);
	readonly ConcurrentDictionary<string, string> _registeredAggregateTypes = new(StringComparer.InvariantCulture);

	public string GetName<T>(IEvent @event)
		where T : IAggregate
		=> GetName<T>(@event.GetType());

	public string GetName<T>(Type aggregateEventType)
		where T : IAggregate
	{
		var eventTypeAssemblyQualifiedName = aggregateEventType.AssemblyQualifiedName.OrDefault(aggregateEventType.ToString());
		if (!_eventNamesByAssemblyTypeName.TryGetValue(eventTypeAssemblyQualifiedName, out var eventName))
		{
			eventName = TypeNameHelper.GetName(aggregateEventType, "Event", true);

			if (eventName != aggregateEventType.FullName)
			{
				var aggregateType = typeof(T).FullName!;
				if (!_registeredAggregateTypes.TryGetValue(aggregateType, out var aggregateName))
					throw new InvalidOperationException($"{aggregateType} has not been registered, call InitializeAggregate.");

				eventName = $"{aggregateName}.{eventName}";
			}

			if (_eventNamesByAssemblyTypeName.TryAdd(eventTypeAssemblyQualifiedName, eventName))
				_eventNamesByDefinedTypeName.TryAdd(eventName, eventTypeAssemblyQualifiedName);
		}

		return eventName;
	}

	public string? GetTypeName<T>(string eventTypeName)
		where T : IAggregate
	{
		ArgumentNullException.ThrowIfNull(eventTypeName.OrNull(), nameof(eventTypeName));

		return _eventNamesByDefinedTypeName.TryGetValue(eventTypeName, out var eventName)
			? eventName
			: null;
	}

	public string InitializeAggregate<T>()
		where T : class, IAggregate, new()
	{
		var aggregateType = typeof(T);
		var aggregateTypeFullName = aggregateType.FullName!;

		return _registeredAggregateTypes.GetOrAdd(aggregateTypeFullName, _ =>
		{
			var aggregateInstance = new T();
			var aggregateName = aggregateInstance.AggregateType;

			Populate<T>(aggregateName, [.. aggregateInstance.GetRegisteredEventTypes()]);

			return aggregateName;
		});
	}

	void Populate<T>(string aggregateName, Type[] aggregateEvents)
		where T : IAggregate
	{
		if (aggregateEvents == null || aggregateEvents.Length == 0)
			return;

		for (var i = 0; i < aggregateEvents.Length; i++)
		{
			var aggregateEventType = aggregateEvents[i];
			var eventTypeAssemblyQualifiedName = aggregateEventType.AssemblyQualifiedName.OrDefault(aggregateEventType.ToString());
			if (!_eventNamesByAssemblyTypeName.TryGetValue(eventTypeAssemblyQualifiedName, out var _))
			{
				var eventName = TypeNameHelper.GetName(aggregateEventType, "Event", true);
				if (eventName != aggregateEventType.FullName)
					eventName = $"{aggregateName}.{eventName}";

				if (_eventNamesByAssemblyTypeName.TryAdd(eventTypeAssemblyQualifiedName, eventName))
					_eventNamesByDefinedTypeName.TryAdd(eventName, eventTypeAssemblyQualifiedName);
			}
		}
	}
}
