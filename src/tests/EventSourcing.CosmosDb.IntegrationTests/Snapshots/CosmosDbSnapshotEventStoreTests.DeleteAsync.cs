using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.CosmosDb.Snapshots;

partial class CosmosDbSnapshotEventStoreTests
{
	[Test]
	public async Task DeleteAsync_GivenExistingAggregateMarkedAsDeleted_DeletesFromCosmosDb(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		PartitionKey partitionKey = new(aggregate.AggregateType);

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await Assert.That(saveResult).IsTrue();

		var aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: cancellationToken
		);
		await Assert.That(aggregateFromCosmosDb).IsNotNull();

		// Act
		var deleteResult = await context.EventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(deleteResult).IsTrue();
		await Assert.That(aggregateFromCosmosDb).IsNull();
	}
}
