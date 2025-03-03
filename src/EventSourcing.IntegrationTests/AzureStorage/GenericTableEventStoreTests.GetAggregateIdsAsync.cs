namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(int aggregateCount)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		List<string> generatedIds = [];
		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: aggregateCount);

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();

			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			generatedIds.Add(aggregateId);
		}

		// Act
		var returnedTypes = eventStore
			.GetAggregateIdsAsync(true, cancellationToken: tokenSource.Token)
			.ToBlockingEnumerable(tokenSource.Token)
		;

		// Assert
		returnedTypes.ShouldHaveCount(aggregateCount);
		generatedIds.ShouldBe(returnedTypes, ignoreOrder: true);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		List<string> generatedIds = [];
		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: nonDeletedAggregateIdCount + (deletedAggregateIdCount * 2));

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();

			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			generatedIds.Add(aggregateId);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();

			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
			await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		}

		// Act
		var returnedTypes = eventStore.GetAggregateIdsAsync(false, cancellationToken: tokenSource.Token).ToBlockingEnumerable(tokenSource.Token);

		// Assert
		returnedTypes.ShouldHaveCount(nonDeletedAggregateIdCount);
		generatedIds.ShouldBeEquivalentTo(returnedTypes);
	}

	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		List<string> generatedIds = [];
		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: nonDeletedAggregateIdCount + (deletedAggregateIdCount * 2));

		for (var i = 0; i < nonDeletedAggregateIdCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();

			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			generatedIds.Add(aggregateId);
		}

		for (var i = 0; i < deletedAggregateIdCount; i++)
		{
			var aggregateId = $"{Guid.NewGuid()}";
			var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
			aggregate.IncrementInt32Value();

			await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
			await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

			generatedIds.Add(aggregateId);
		}

		// Act
		var returnedTypes = eventStore.GetAggregateIdsAsync(true, cancellationToken: tokenSource.Token).ToBlockingEnumerable(tokenSource.Token);

		// Assert
		returnedTypes.ShouldHaveCount(deletedAggregateIdCount + nonDeletedAggregateIdCount);
		generatedIds.ShouldBe(returnedTypes, ignoreOrder: true);
	}
}
