namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
{
	[Test]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInMongoDBWhenRestore_SnapshotCreatedInMongoDB()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		var mongoDbEventStore = context.EventStore;

		bool saveResult = await mongoDbEventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(saveResult).IsTrue();

		var predicate = PredicateId(aggregateId);

		var aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);
		await Assert.That(aggregateFromMongo).IsNotNull();

		var deleteResult = await mongoDbEventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(deleteResult).IsTrue();

		aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);
		await Assert.That(aggregateFromMongo).IsNull();

		// Act
		var restoreResult = await mongoDbEventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromMongo = await context.MongoDBClient.GetAsync(predicate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(restoreResult).IsTrue();
		await Assert.That(aggregateFromMongo).IsNotNull();
	}
}
