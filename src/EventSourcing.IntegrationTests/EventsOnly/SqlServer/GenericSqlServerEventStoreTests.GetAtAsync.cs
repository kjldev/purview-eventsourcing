namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		int previousEventsToCreate
	)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < previousEventsToCreate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Add more events
		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.GetAtAsync(
			aggregateId,
			previousEventsToCreate,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IncrementInt32).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.Locked).IsTrue();
	}
}
