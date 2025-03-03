namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(int previousEventsToCreate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < previousEventsToCreate; i++)
			aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act

		// Add an extra event to push it past the requested number of events.
		aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregate.IncrementInt32.ShouldBe(previousEventsToCreate + 1);

		// Assert
		var result = await eventStore.GetAtAsync(aggregateId, version: previousEventsToCreate, cancellationToken: tokenSource.Token);

		result.ShouldNotBeNull();
		result.IncrementInt32.ShouldBe(previousEventsToCreate);
		result.Details.SavedVersion.ShouldBe(previousEventsToCreate);
		result.Details.CurrentVersion.ShouldBe(previousEventsToCreate);
		result.Details.Locked.ShouldBeTrue();
	}
}
