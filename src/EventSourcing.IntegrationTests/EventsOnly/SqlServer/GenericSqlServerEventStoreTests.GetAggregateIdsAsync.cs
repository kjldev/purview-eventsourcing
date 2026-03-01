namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(int aggregateCount)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var expectedIds = new List<string>();
		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			expectedIds.Add(aggregateId);
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(false, cancellationToken: tokenSource.Token))
			ids.Add(id);

		ids.Count.ShouldBe(aggregateCount);
		foreach (var expectedId in expectedIds)
			ids.ShouldContain(expectedId);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
			await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(true, cancellationToken: tokenSource.Token))
			ids.Add(id);

		ids.Count.ShouldBe(nonDeletedAggregateIdCount + deletedAggregateIdCount);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
			await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(false, cancellationToken: tokenSource.Token))
			ids.Add(id);

		ids.Count.ShouldBe(nonDeletedAggregateIdCount);
	}
}
