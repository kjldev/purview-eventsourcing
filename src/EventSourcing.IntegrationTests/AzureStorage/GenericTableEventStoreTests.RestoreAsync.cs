namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 3);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregate = await eventStore.GetDeletedAsync(aggregateId);
		aggregate.ShouldNotBeNull();

		// Act
		var result = await eventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.Details.IsDeleted.ShouldBeFalse();
		aggregate.Details.SavedVersion.ShouldBe(3);
	}
}
