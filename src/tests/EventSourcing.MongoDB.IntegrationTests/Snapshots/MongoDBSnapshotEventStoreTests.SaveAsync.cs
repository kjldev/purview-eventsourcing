namespace Purview.EventSourcing.MongoDB.Snapshots;

partial class MongoDBSnapshotEventStoreTests
{
	[Test]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(CancellationToken cancellationToken)
	{
		// Arrange
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();
		aggregate.AppendString(aggregateId);

		var mongoDbEventStore = context.EventStore;

		// Act
		bool result = await mongoDbEventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		var builder = PredicateId(aggregateId);

		// Verify by re-getting the aggregate directly from the MongoClient, not via the event store.
		var aggregateFromMongoDB = await context.MongoDBClient.GetAsync(builder, cancellationToken: cancellationToken);

		await Assert.That(aggregateFromMongoDB).IsNotNull();
		await Assert.That(aggregateFromMongoDB.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromMongoDB.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromMongoDB.StringProperty).IsEqualTo(aggregateId);
		await Assert.That(aggregateFromMongoDB.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromMongoDB.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromMongoDB.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);
		await Assert.That(aggregateFromMongoDB.Details.Etag).IsEqualTo(aggregate.Details.Etag);
	}
}
