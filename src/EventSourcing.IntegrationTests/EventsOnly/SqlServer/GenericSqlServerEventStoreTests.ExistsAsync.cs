namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task ExistsAsync_GivenSavedAggregate_ReturnsExists()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.ExistsAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert.That(result.DoesExist).IsTrue();
		await Assert.That(result.Status).IsEqualTo(ExistsStatus.Exists);
	}
}
