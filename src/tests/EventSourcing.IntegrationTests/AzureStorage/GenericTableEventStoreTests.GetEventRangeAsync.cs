using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
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
		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var eventStore = fixture.CreateEventStore<TAggregate>();

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
		await foreach ((var @event, _) in results)
			await Assert.That(@event.Details.AggregateVersion).IsEqualTo(startEvent++);
	}
}
