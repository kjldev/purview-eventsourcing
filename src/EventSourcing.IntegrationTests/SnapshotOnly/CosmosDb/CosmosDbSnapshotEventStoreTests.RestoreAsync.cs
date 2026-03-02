using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Test]
	public async Task RestoreAsync_GivenExistingAggregateMarkedAsDeletedAndDoesNotExistInCosmosDbWhenRestore_SnapshotCreatedInCosmosDb()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		PartitionKey partitionKey = new(aggregate.AggregateType);

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(saveResult).IsTrue();

		var aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(aggregateFromCosmosDb).IsNotNull();

		var deleteResult = await context.EventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(deleteResult).IsTrue();

		aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(aggregateFromCosmosDb).IsNull();

		// Act
		var restoreResult = await context.EventStore.RestoreAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(
			aggregateId,
			partitionKey,
			cancellationToken: tokenSource.Token
		);

		// Assert
		await Assert.That(restoreResult).IsTrue();
		await Assert.That(aggregateFromCosmosDb).IsNotNull();
	}
}
