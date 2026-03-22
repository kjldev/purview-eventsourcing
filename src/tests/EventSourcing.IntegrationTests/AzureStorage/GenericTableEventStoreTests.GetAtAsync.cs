namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		int previousEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < previousEventsToCreate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act

		// Add an extra event to push it past the requested number of events.
		aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(aggregate.IncrementInt32).IsEqualTo(previousEventsToCreate + 1);

		// Assert
		var result = await eventStore.GetAtAsync(
			aggregateId,
			version: previousEventsToCreate,
			cancellationToken: cancellationToken
		);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IncrementInt32).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.Locked).IsTrue();
	}
}
