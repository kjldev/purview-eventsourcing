using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Fact]
	public async Task DeleteAsync_GivenExistingAggregateMarkedAsDeleted_DeletesFromCosmosDb()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		PartitionKey partitionKey = new(aggregate.AggregateType);

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		saveResult.ShouldBeTrue();

		var aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(aggregateId, partitionKey, cancellationToken: tokenSource.Token);
		aggregateFromCosmosDb.ShouldNotBeNull();

		// Act
		var deleteResult = await context.EventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(aggregateId, partitionKey, cancellationToken: tokenSource.Token);

		// Assert
		deleteResult.ShouldBeTrue();
		aggregateFromCosmosDb.ShouldBeNull();
	}
}
