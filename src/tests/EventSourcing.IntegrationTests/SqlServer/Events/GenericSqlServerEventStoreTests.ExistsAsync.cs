namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task ExistsAsync_GivenSavedAggregate_ReturnsExists(CancellationToken cancellationToken)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.ExistsAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result.DoesExist).IsTrue();
		await Assert.That(result.Status).IsEqualTo(ExistsStatus.Exists);
	}
}
