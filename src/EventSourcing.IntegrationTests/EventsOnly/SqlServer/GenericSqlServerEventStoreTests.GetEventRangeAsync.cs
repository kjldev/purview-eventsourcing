namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
		int eventsToCreate,
		int startEvent,
		int? endEvent
	)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var events = new List<(Aggregates.Events.IEvent @event, string eventType)>();
		await foreach (var e in eventStore.GetEventRangeAsync(aggregateId, startEvent, endEvent, tokenSource.Token))
			events.Add(e);

		// Verify events are in order
		for (var i = 1; i < events.Count; i++)
			await Assert
				.That(events[i].@event.Details.AggregateVersion)
				.IsGreaterThan(events[i - 1].@event.Details.AggregateVersion);
	}

	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		int expectedEventCount
	)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var events = new List<(Aggregates.Events.IEvent @event, string eventType)>();
		await foreach (var e in eventStore.GetEventRangeAsync(aggregateId, startEvent, endEvent, tokenSource.Token))
			events.Add(e);

		await Assert.That(events.Count).IsEqualTo(expectedEventCount);
	}
}
