namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var aggregateResult = await eventStore.GetDeletedAsync(aggregateId, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult.Details.IsDeleted).IsTrue();
		await Assert.That(aggregateResult.Details.SavedVersion).IsEqualTo(2);
	}
}
