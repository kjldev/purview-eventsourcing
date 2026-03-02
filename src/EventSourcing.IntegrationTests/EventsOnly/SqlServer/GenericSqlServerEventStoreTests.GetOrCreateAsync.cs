namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.GetOrCreateAsync(null, null, cancellationToken: tokenSource.Token);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsTrue();
	}
}
