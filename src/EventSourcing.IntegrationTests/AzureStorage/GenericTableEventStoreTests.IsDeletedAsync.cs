namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();
	}

	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsFalse();
	}
}
