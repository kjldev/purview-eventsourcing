namespace Purview.EventSourcing.SqlServer.Events;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(CancellationToken cancellationToken)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.GetDeletedAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.Details.IsDeleted).IsTrue();
	}
}
