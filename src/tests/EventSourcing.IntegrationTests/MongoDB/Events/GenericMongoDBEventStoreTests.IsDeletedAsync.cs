namespace Purview.EventSourcing.MongoDB.Events;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
	}

	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var result = await eventStore.IsDeletedAsync(aggregateId, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsFalse();
	}
}
