namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
{
	[Fact]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInMongoDBWhenRestore_SnapshotCreatedInMongoDB()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		var mongoDbEventStore = context.EventStore;

		bool saveResult = await mongoDbEventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		saveResult.ShouldBeTrue();

		var predicate = PredicateId(aggregateId);

		var aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);
		aggregateFromMongo.ShouldNotBeNull();

		var deleteResult = await mongoDbEventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		deleteResult.ShouldBeTrue();

		aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);
		aggregateFromMongo.ShouldBeNull();

		// Act
		var restoreResult = await mongoDbEventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);

		// Assert
		restoreResult.ShouldBeTrue();
		aggregateFromMongo.ShouldNotBeNull();
	}
}
