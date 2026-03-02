namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		int previousEventsToCreate
	)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < previousEventsToCreate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act

		// Add an extra event to push it past the requested number of events.
		aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		await Assert.That(aggregate.IncrementInt32).IsEqualTo(previousEventsToCreate + 1);

		// Assert
		var result = await eventStore.GetAtAsync(
			aggregateId,
			version: previousEventsToCreate,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IncrementInt32).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.Locked).IsTrue();
	}
}
