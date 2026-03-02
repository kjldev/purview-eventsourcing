namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 3);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregate = await eventStore.GetDeletedAsync(aggregateId);
		await Assert.That(aggregate).IsNotNull();

		// Act
		var result = await eventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.Details.IsDeleted).IsFalse();
		await Assert.That(aggregate.Details.SavedVersion).IsEqualTo(3);
	}
}
