using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		int expectedEventCount,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var results = eventStore.GetEventRangeAsync(
			aggregateId,
			startEvent,
			endEvent,
			cancellationToken: cancellationToken
		);

		// Assert
		List<IEvent> eventList = [];
		await foreach ((var @event, _) in results)
			eventList.Add(@event);

		await Assert.That(eventList.Count).IsEqualTo(expectedEventCount);
	}

	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		using var eventStore = ctx.EventStore;
		var eventClient = ctx.EventClient;
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		List<(IEvent @event, string eventType)> resultsList = [];
		await foreach (
			var item in eventStore.GetEventRangeAsync(
				aggregateId,
				startEvent,
				endEvent,
				cancellationToken: cancellationToken
			)
		)
			resultsList.Add(item);
		var results = resultsList.ToArray();
		var continuationResult = await eventClient.QueryAsync<EventEntity>(
			m => m.AggregateId == aggregateId && m.EntityType == EntityTypes.EventType,
			e => e.OrderBy(m => m.Version),
			eventsToCreate,
			cancellationToken
		);

		// Assert
		await Assert.That(continuationResult.ResultCount).IsEqualTo(eventsToCreate);

		List<IEvent> eventList = [];
		foreach ((var @event, _) in results)
			await Assert.That(@event.Details.AggregateVersion).IsEqualTo(startEvent++);
	}
}
