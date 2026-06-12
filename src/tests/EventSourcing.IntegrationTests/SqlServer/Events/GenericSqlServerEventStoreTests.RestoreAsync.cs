namespace Purview.EventSourcing.SqlServer.Events;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(
		CancellationToken cancellationToken
	)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.RestoreAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.Details.IsDeleted).IsFalse();
	}
}
