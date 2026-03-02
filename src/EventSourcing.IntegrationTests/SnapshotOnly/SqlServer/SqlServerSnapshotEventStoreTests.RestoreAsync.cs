using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInSqlServerWhenRestore_SnapshotCreatedInSqlServer()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(saveResult).IsTrue();

		var aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(aggregateFromSqlServer).IsNotNull();

		var deleteResult = await context.EventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(deleteResult).IsTrue();

		aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(aggregateFromSqlServer).IsNull();

		// Act
		var restoreResult = await context.EventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: tokenSource.Token
		);

		// Assert
		await Assert.That(restoreResult).IsTrue();
		await Assert.That(aggregateFromSqlServer).IsNotNull();
	}
}
