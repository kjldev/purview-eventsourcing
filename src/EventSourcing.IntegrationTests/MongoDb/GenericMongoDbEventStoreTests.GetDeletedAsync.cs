namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var aggregateResult = await eventStore.GetDeletedAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult.Details.IsDeleted).IsTrue();
		await Assert.That(aggregateResult.Details.SavedVersion).IsEqualTo(2);
	}
}
