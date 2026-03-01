namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		result.ShouldBeTrue();
		aggregate.Details.IsDeleted.ShouldBeFalse();
	}
}
