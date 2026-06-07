using System.Text;
using Purview.EventSourcing.MongoDB.Entities;
using Purview.EventSourcing.MongoDB.StorageClients;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
    public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var eventStore = fixture.CreateEventStore<TAggregate>();
        var eventClient = fixture.EventClient;
        var snapshotClient = fixture.SnapshotClient;

        await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);
        await Assert.That(aggregate).IsNotNull();

        // Act
        var result = await eventStore.DeleteAsync(
            aggregate!,
            new EventStoreOperationContext { PermanentlyDelete = true },
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(aggregate.Details.IsDeleted).IsTrue();
        await Assert.That(aggregate.Details.Locked).IsTrue();

        await ValidateEntitiesDeletedAsync(
            aggregate,
            eventClient,
            snapshotClient,
            cancellationToken
        );
    }

    public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var aggregateId = $"{Guid.NewGuid()}";
        var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
        aggregate.IncrementInt32Value();

        var value = string.Empty;
        var sizeIsLessThan32K = true;
        while (sizeIsLessThan32K)
        {
            value += "abcdefghijklmnopqrstvwxyz";
            value += "ABCDEFGHIJKLMNOPQRSTVWXYZ";
            value += "1234567890";

            sizeIsLessThan32K = Encoding.UTF8.GetByteCount(value) < short.MaxValue;
        }

        aggregate.AppendString(value);

        var eventStore = fixture.CreateEventStore<TAggregate>();
        var eventClient = fixture.EventClient;
        var snapshotClient = fixture.SnapshotClient;

        await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

        aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);
        await Assert.That(aggregate).IsNotNull();

        // Act
        var result = await eventStore.DeleteAsync(
            aggregate!,
            new EventStoreOperationContext { PermanentlyDelete = true },
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(result).IsTrue();

        await Assert.That(aggregate.Details.IsDeleted).IsTrue();
        await Assert.That(aggregate.Details.Locked).IsTrue();

        await ValidateEntitiesDeletedAsync(
            aggregate,
            eventClient,
            snapshotClient,
            cancellationToken
        );
    }

    async Task ValidateEntitiesDeletedAsync(
        TAggregate aggregate,
        MongoDBClient eventClient,
        MongoDBClient snapshotClient,
        CancellationToken cancellationToken
    )
    {
        var eventCount = await eventClient.CountAsync<EventEntity>(
            m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.EventType,
            cancellationToken: cancellationToken
        );
        await Assert.That(eventCount).IsEqualTo(0);

        var streamVersionCount = await eventClient.CountAsync<StreamVersionEntity>(
            m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.StreamVersionType,
            cancellationToken: cancellationToken
        );
        await Assert.That(streamVersionCount).IsEqualTo(0);

        var idempotencyMarkerCount = await eventClient.CountAsync<IdempotencyMarkerEntity>(
            m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.StreamVersionType,
            cancellationToken: cancellationToken
        );
        await Assert.That(idempotencyMarkerCount).IsEqualTo(0);

        var snapshotEntity = await snapshotClient.GetAsync<SnapshotEntity>(
            m => m.Id == aggregate.Id() && m.EntityType == EntityTypes.SnapshotType,
            cancellationToken: cancellationToken
        );
        await Assert.That(snapshotEntity).IsNull();
    }
}
