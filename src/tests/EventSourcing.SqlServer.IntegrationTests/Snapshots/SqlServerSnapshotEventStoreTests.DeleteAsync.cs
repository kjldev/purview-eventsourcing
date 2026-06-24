using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshots;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task DeleteAsync_GivenExistingAggregateMarkedAsDeleted_DeletesFromSqlServer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await Assert.That(saveResult).IsTrue();

		var aggregateFromSqlServer = await store.GetAsync<PersistenceAggregate>(
			aggregateId,
			new EventStoreOperationContext { SnapshotCacheMode = SnapshotCachingOptions.None },
			cancellationToken: cancellationToken
		);
		await Assert.That(aggregateFromSqlServer).IsNotNull();

		// Act
		var deleteResult = await store.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		aggregateFromSqlServer = await store.GetAsync<PersistenceAggregate>(
			aggregateId,
			new EventStoreOperationContext { SnapshotCacheMode = SnapshotCachingOptions.None },
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(deleteResult).IsTrue();
		await Assert.That(aggregateFromSqlServer).IsNull();
	}
}
