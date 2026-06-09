namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		int previousEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < previousEventsToCreate; i++)
			aggregate.IncrementInt32Value();
		var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Add more events
		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.GetAtAsync(
			aggregateId,
			previousEventsToCreate,
			cancellationToken: cancellationToken
		);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IncrementInt32).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(previousEventsToCreate);
		await Assert.That(result.Details.Locked).IsTrue();
	}
}
