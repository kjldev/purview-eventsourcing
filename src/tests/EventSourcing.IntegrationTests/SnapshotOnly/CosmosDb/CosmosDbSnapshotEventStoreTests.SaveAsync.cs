using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Test]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(CancellationToken cancellationToken)
	{
		// Arrange
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		PartitionKey partitionKey = new(aggregate.AggregateType);

		// Act
		bool result = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: cancellationToken
		);

		await Assert.That(aggregateFromCosmosDb).IsNotNull();
		await Assert.That(aggregateFromCosmosDb.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromCosmosDb.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromCosmosDb.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromCosmosDb.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromCosmosDb.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);
		await Assert.That(aggregateFromCosmosDb.Details.Etag).IsEqualTo(aggregate.Details.Etag);
	}
}
