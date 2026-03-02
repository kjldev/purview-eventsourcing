using Purview.EventSourcing.ChangeFeed;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var aggregateResult =
			await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token)
			?? throw new NullReferenceException();

		// Act
		var result = await eventStore.DeleteAsync(aggregateResult, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregateResult.Details.IsDeleted).IsTrue();
		await Assert.That(aggregateResult.Details.SavedVersion).IsEqualTo(2);
	}

	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>(
			correlationIdsToGenerate: 2,
			removeFromCacheOnDelete: true
		);

		var cacheKey = eventStore.CreateCacheKey(aggregateId);

		await eventStore.SaveAsync(aggregate, tokenSource.Token);

		var aggregateResult =
			await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token)
			?? throw new NullReferenceException();

		// Act
		var result = await eventStore.DeleteAsync(aggregateResult, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();

		await fixture.Cache.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
	}

	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed()
	{
		// Arrange
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();

		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var beforeWasCalled = false;
		var afterWasCalled = false;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

		aggregateChangeNotifier
			.When(m => m.BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>()))
			.Do(_ => beforeWasCalled = true);

		aggregateChangeNotifier
			.When(m => m.AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>()))
			.Do(_ => afterWasCalled = true);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var result = await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(beforeWasCalled).IsTrue();
		await Assert.That(afterWasCalled).IsTrue();

		await aggregateChangeNotifier.Received(1).BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>());

		await aggregateChangeNotifier.Received(1).AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>());
	}
}
