using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
{
    [Test]
    public async Task DeleteAsync_GivenExistingAggregateMarkedAsDeleted_DeletesFromMongoDB(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var context = fixture.CreateContext();

        var aggregateId = Guid.NewGuid().ToString();
        var aggregate = CreateAggregate(id: aggregateId);
        aggregate.IncrementInt32Value();

        var mongoDbEventStore = context.EventStore;

        bool saveResult = await mongoDbEventStore.SaveAsync(
            aggregate,
            cancellationToken: cancellationToken
        );
        await Assert.That(saveResult).IsTrue();

        var predicate = PredicateId(aggregateId);
        var aggregateFromMongoDB = await context.MongoDBClient.GetAsync(
            predicate,
            cancellationToken: cancellationToken
        );
        await Assert.That(aggregateFromMongoDB).IsNotNull();

        // Act
        var deleteResult = await mongoDbEventStore.DeleteAsync(
            aggregate,
            cancellationToken: cancellationToken
        );

        aggregateFromMongoDB = await context.MongoDBClient.GetAsync<PersistenceAggregate>(
            a => a.Details.Id == aggregateId,
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(deleteResult).IsTrue();
        await Assert.That(aggregateFromMongoDB).IsNull();
    }
}
