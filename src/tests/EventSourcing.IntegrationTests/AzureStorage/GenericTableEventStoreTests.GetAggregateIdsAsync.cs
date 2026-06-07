namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
    public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
        int aggregateCount,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        List<string> generatedIds = [];
        var eventStore = fixture.CreateEventStore<TAggregate>();

        for (var i = 0; i < aggregateCount; i++)
        {
            var aggregateId = $"{Guid.NewGuid()}";
            var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
            aggregate.IncrementInt32Value();

            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

            generatedIds.Add(aggregateId);
        }

        // Act
        List<string> returnedTypes = [];
        await foreach (
            var id in eventStore.GetAggregateIdsAsync(true, cancellationToken: cancellationToken)
        )
            returnedTypes.Add(id);

        // Assert
        await Assert.That(returnedTypes.Count).IsEqualTo(aggregateCount);
        await Assert.That(generatedIds).IsEquivalentTo(returnedTypes);
    }

    public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
        int nonDeletedAggregateIdCount,
        int deletedAggregateIdCount,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        List<string> generatedIds = [];
        var eventStore = fixture.CreateEventStore<TAggregate>();

        for (var i = 0; i < nonDeletedAggregateIdCount; i++)
        {
            var aggregateId = $"{Guid.NewGuid()}";
            var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
            aggregate.IncrementInt32Value();

            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

            generatedIds.Add(aggregateId);
        }

        for (var i = 0; i < deletedAggregateIdCount; i++)
        {
            var aggregateId = $"{Guid.NewGuid()}";
            var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
            aggregate.IncrementInt32Value();

            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
            await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);
        }

        // Act
        List<string> returnedTypes = [];
        await foreach (
            var id in eventStore.GetAggregateIdsAsync(false, cancellationToken: cancellationToken)
        )
            returnedTypes.Add(id);

        // Assert
        await Assert.That(returnedTypes.Count).IsEqualTo(nonDeletedAggregateIdCount);
        await Assert.That(generatedIds).IsEquivalentTo(returnedTypes);
    }

    public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
        int nonDeletedAggregateIdCount,
        int deletedAggregateIdCount,
        CancellationToken cancellationToken
    )
    {
        // Arrange
        List<string> generatedIds = [];
        var eventStore = fixture.CreateEventStore<TAggregate>();

        for (var i = 0; i < nonDeletedAggregateIdCount; i++)
        {
            var aggregateId = $"{Guid.NewGuid()}";
            var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
            aggregate.IncrementInt32Value();

            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

            generatedIds.Add(aggregateId);
        }

        for (var i = 0; i < deletedAggregateIdCount; i++)
        {
            var aggregateId = $"{Guid.NewGuid()}";
            var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
            aggregate.IncrementInt32Value();

            await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
            await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

            generatedIds.Add(aggregateId);
        }

        // Act
        List<string> returnedTypes = [];
        await foreach (
            var id in eventStore.GetAggregateIdsAsync(true, cancellationToken: cancellationToken)
        )
            returnedTypes.Add(id);

        // Assert
        await Assert
            .That(returnedTypes.Count)
            .IsEqualTo(deletedAggregateIdCount + nonDeletedAggregateIdCount);
        await Assert.That(generatedIds).IsEquivalentTo(returnedTypes);
    }
}
