namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(int previousEventsToCreate)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
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

		var result = await eventStore.GetAtAsync(aggregateId, previousEventsToCreate, cancellationToken: tokenSource.Token);

		result.ShouldNotBeNull();
		result.IncrementInt32.ShouldBe(previousEventsToCreate);
		result.Details.CurrentVersion.ShouldBe(previousEventsToCreate);
		result.Details.Locked.ShouldBeTrue();
	}
}
