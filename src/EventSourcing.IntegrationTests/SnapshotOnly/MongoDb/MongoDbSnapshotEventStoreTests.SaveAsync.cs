namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
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

		var mongoDbEventStore = context.EventStore;

		// Act
		bool result = await mongoDbEventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		var builder = PredicateId(aggregateId);

		// Verify by re-getting the aggregate directly from the MongoClient, not via the event store.
		var aggregateFromMongoDB = await context.MongoDBClient.GetAsync(builder, cancellationToken: tokenSource.Token);

		aggregateFromMongoDB.ShouldNotBeNull();
		aggregateFromMongoDB.Id().ShouldBe(aggregate.Id());
		aggregateFromMongoDB.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromMongoDB.StringProperty.ShouldBe(aggregateId);
		aggregateFromMongoDB.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromMongoDB.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromMongoDB.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);
		aggregateFromMongoDB.Details.Etag.ShouldBe(aggregate.Details.Etag);
	}
}
