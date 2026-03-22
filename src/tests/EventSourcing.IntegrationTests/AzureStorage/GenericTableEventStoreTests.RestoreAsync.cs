namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 3);

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		aggregate = await eventStore.GetDeletedAsync(aggregateId, cancellationToken);
		await Assert.That(aggregate).IsNotNull();

		// Act
		var result = await eventStore.RestoreAsync(aggregate!, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.Details.IsDeleted).IsFalse();
		await Assert.That(aggregate.Details.SavedVersion).IsEqualTo(3);
	}
}
