namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
		int aggregateCount,
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<TAggregate>();

		var expectedIds = new List<string>();
		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			expectedIds.Add(aggregateId);
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(false, cancellationToken: cancellationToken))
			ids.Add(id);

		await Assert.That(ids.Count).IsEqualTo(aggregateCount);
		foreach (var expectedId in expectedIds)
			await Assert.That(ids).Contains(expectedId);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<TAggregate>();

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
			await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(true, cancellationToken: cancellationToken))
			ids.Add(id);

		await Assert.That(ids.Count).IsEqualTo(nonDeletedAggregateIdCount + deletedAggregateIdCount);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<TAggregate>();

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: $"{Guid.NewGuid()}");
			aggregate.IncrementInt32Value();
			await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
			await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);
		}

		var ids = new List<string>();
		await foreach (var id in eventStore.GetAggregateIdsAsync(false, cancellationToken: cancellationToken))
			ids.Add(id);

		await Assert.That(ids.Count).IsEqualTo(nonDeletedAggregateIdCount);
	}
}
