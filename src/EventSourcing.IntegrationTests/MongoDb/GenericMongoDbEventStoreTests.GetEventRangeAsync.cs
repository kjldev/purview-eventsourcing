using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(int eventsToCreate, int startEvent, int? endEvent, int expectedEventCount)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var results = eventStore.GetEventRangeAsync(aggregateId, startEvent, endEvent, cancellationToken: tokenSource.Token);

		// Assert
		List<IEvent> eventList = [];
		await foreach ((var @event, _) in results)
			eventList.Add(@event);

		eventList.ShouldHaveCount(expectedEventCount);
	}

	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(int eventsToCreate, int startEvent, int? endEvent)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var results = eventStore.GetEventRangeAsync(aggregateId, startEvent, endEvent, cancellationToken: tokenSource.Token).ToBlockingEnumerable().ToArray();
		var continuationResult = await fixture.EventClient.QueryAsync<EventEntity>(
			m => m.AggregateId == aggregateId && m.EntityType == EntityTypes.EventType,
			 e => e.OrderBy(m => m.Version),
			 eventsToCreate,
			 tokenSource.Token);

		// Assert
		continuationResult.ResultCount.ShouldBe(eventsToCreate);

		List<IEvent> eventList = [];
		foreach ((var @event, _) in results)
			@event.Details.AggregateVersion.ShouldBe(++startEvent);
	}
}
