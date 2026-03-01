using Purview.EventSourcing.ChangeFeed;

namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		var aggregateResult = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token) ?? throw new NullReferenceException();

		var result = await eventStore.DeleteAsync(aggregateResult, cancellationToken: tokenSource.Token);

		result.ShouldBeTrue();
		aggregateResult.Details.IsDeleted.ShouldBeTrue();
		aggregateResult.Details.SavedVersion.ShouldBe(2);
	}

	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2, removeFromCacheOnDelete: true);
		var cacheKey = eventStore.CreateCacheKey(aggregateId);
		await eventStore.SaveAsync(aggregate, tokenSource.Token);
		var aggregateResult = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token) ?? throw new NullReferenceException();

		var result = await eventStore.DeleteAsync(aggregateResult, cancellationToken: tokenSource.Token);

		result.ShouldBeTrue();
		await fixture.Cache.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
	}

	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed()
	{
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var beforeWasCalled = false;
		var afterWasCalled = false;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);
		aggregateChangeNotifier.When(m => m.BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>())).Do(_ => beforeWasCalled = true);
		aggregateChangeNotifier.When(m => m.AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>())).Do(_ => afterWasCalled = true);
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		beforeWasCalled.ShouldBeTrue();
		afterWasCalled.ShouldBeTrue();
		await aggregateChangeNotifier.Received(1).BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>());
		await aggregateChangeNotifier.Received(1).AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>());
	}

	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.DeleteAsync(aggregate, new EventStoreOperationContext { PermanentlyDelete = true }, cancellationToken: tokenSource.Token);

		result.ShouldBeTrue();
		aggregate.Details.IsDeleted.ShouldBeTrue();
		aggregate.Details.Locked.ShouldBeTrue();

		// Verify all data was removed
		var exists = await eventStore.ExistsAsync(aggregateId, cancellationToken: tokenSource.Token);
		exists.Status.ShouldBe(ExistsStatus.DoesNotExist);
	}
}
