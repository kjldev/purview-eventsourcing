using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshots;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInSqlServerWhenRestore_SnapshotCreatedInSqlServer(
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
			cancellationToken: cancellationToken
		);
		await Assert.That(aggregateFromSqlServer).IsNotNull();

		var deleteResult = await store.DeleteAsync(aggregate, cancellationToken: cancellationToken);
		await Assert.That(deleteResult).IsTrue();

		aggregateFromSqlServer = await store.GetAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: cancellationToken
		);
		await Assert.That(aggregateFromSqlServer).IsNull();

		// Act
		var restoreResult = await store.RestoreAsync(aggregate, cancellationToken: cancellationToken);

		aggregateFromSqlServer = await store.GetAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(restoreResult).IsTrue();
		await Assert.That(aggregateFromSqlServer).IsNotNull();
	}
}
