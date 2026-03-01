using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Fact]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInSqlServerWhenRestore_SnapshotCreatedInSqlServer()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		saveResult.ShouldBeTrue();

		var aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);
		aggregateFromSqlServer.ShouldNotBeNull();

		var deleteResult = await context.EventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		deleteResult.ShouldBeTrue();

		aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);
		aggregateFromSqlServer.ShouldBeNull();

		// Act
		var restoreResult = await context.EventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		restoreResult.ShouldBeTrue();
		aggregateFromSqlServer.ShouldNotBeNull();
	}
}
