namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.GetOrCreateAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Id()).IsEqualTo(aggregateId);
		await Assert.That(result.IsNew()).IsTrue();
	}
}
