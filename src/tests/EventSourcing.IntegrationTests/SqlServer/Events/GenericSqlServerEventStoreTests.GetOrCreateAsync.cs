namespace Purview.EventSourcing.SqlServer.Events;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.GetOrCreateAsync(null, null, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsTrue();
	}
}
