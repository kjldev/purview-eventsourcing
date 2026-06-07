using Purview.EventSourcing.ChangeFeed;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
    public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var eventStore = fixture.CreateEventStore<TAggregate>();

        await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        var aggregateResult =
            await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken)
            ?? throw new NullReferenceException();

        // Act
        var result = await eventStore.DeleteAsync(
            aggregateResult,
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(aggregateResult.Details.IsDeleted).IsTrue();
        await Assert.That(aggregateResult.Details.SavedVersion).IsEqualTo(2);
    }

    public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var ctx = fixture.CreateEventStoreContext<TAggregate>(removeFromCacheOnDelete: true);
        var eventStore = ctx.EventStore;
        var cache = ctx.Cache;

        var cacheKey = eventStore.CreateCacheKey(aggregateId);

        await eventStore.SaveAsync(aggregate, cancellationToken);

        var aggregateResult =
            await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken)
            ?? throw new NullReferenceException();

        // Act
        var result = await eventStore.DeleteAsync(
            aggregateResult,
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(result).IsTrue();

        await cache.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
    }

    public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();

        var beforeWasCalled = false;
        var afterWasCalled = false;
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

        aggregateChangeNotifier
            .When(m => m.BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>()))
            .Do(_ => beforeWasCalled = true);

        aggregateChangeNotifier
            .When(m => m.AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>()))
            .Do(_ => afterWasCalled = true);

        await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        // Act
        var result = await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

        // Assert
        await Assert.That(beforeWasCalled).IsTrue();
        await Assert.That(afterWasCalled).IsTrue();

        await aggregateChangeNotifier
            .Received(1)
            .BeforeDeleteAsync(aggregate, Arg.Any<CancellationToken>());

        await aggregateChangeNotifier
            .Received(1)
            .AfterDeleteAsync(aggregate, Arg.Any<CancellationToken>());
    }
}
