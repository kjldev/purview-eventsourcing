using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Fact]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();
		aggregate.AppendString(aggregateId);

		// Act
		bool result = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate directly from SQL Server, not via the event store.
		var aggregateFromSqlServer = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);

		aggregateFromSqlServer.ShouldNotBeNull();
		aggregateFromSqlServer.Id().ShouldBe(aggregate.Id());
		aggregateFromSqlServer.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromSqlServer.StringProperty.ShouldBe(aggregateId);
		aggregateFromSqlServer.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromSqlServer.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromSqlServer.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);
		aggregateFromSqlServer.Details.Etag.ShouldBe(aggregate.Details.Etag);
	}
}
