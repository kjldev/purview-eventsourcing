namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.GetOrCreateAsync(aggregateId, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Id()).IsEqualTo(aggregateId);
		await Assert.That(result.IsNew()).IsTrue();
	}
}
