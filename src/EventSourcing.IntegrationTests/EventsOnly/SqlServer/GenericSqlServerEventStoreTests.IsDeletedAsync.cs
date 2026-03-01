namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: tokenSource.Token);

		result.ShouldBeTrue();
	}

	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: tokenSource.Token);

		result.ShouldBeFalse();
	}
}
