using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
    [Test]
    public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(
        CancellationToken cancellationToken
    )
    {
        // Arrange
        var context = fixture.CreateContext();

        var aggregateId = Guid.NewGuid().ToString();
        var aggregate = CreateAggregate(id: aggregateId);
        aggregate.IncrementInt32Value();
        aggregate.AppendString(aggregateId);

        // Act
        bool result = await context.EventStore.SaveAsync(
            aggregate,
            cancellationToken: cancellationToken
        );

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(aggregate.IsNew()).IsFalse();

        // Verify by re-getting the aggregate directly from SQL Server, not via the event store.
        var aggregateFromSqlServer =
            await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
                aggregateId,
                cancellationToken: cancellationToken
            );

        await Assert.That(aggregateFromSqlServer).IsNotNull();
        await Assert.That(aggregateFromSqlServer.Id()).IsEqualTo(aggregate.Id());
        await Assert
            .That(aggregateFromSqlServer.IncrementInt32)
            .IsEqualTo(aggregate.IncrementInt32);
        await Assert.That(aggregateFromSqlServer.StringProperty).IsEqualTo(aggregateId);
        await Assert
            .That(aggregateFromSqlServer.Details.SavedVersion)
            .IsEqualTo(aggregate.Details.SavedVersion);
        await Assert
            .That(aggregateFromSqlServer.Details.CurrentVersion)
            .IsEqualTo(aggregate.Details.CurrentVersion);
        await Assert
            .That(aggregateFromSqlServer.Details.SnapshotVersion)
            .IsEqualTo(aggregate.Details.SnapshotVersion);
        await Assert.That(aggregateFromSqlServer.Details.Etag).IsEqualTo(aggregate.Details.Etag);
    }
}
