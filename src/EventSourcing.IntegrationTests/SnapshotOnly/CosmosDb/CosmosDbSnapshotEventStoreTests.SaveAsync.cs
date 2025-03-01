using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Fact]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		PartitionKey partitionKey = new(aggregate.AggregateType);

		// Act
		bool result = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromCosmosDb = await context.CosmosDbClient.GetAsync<PersistenceAggregate>(aggregateId, partitionKey, cancellationToken: tokenSource.Token);

		aggregateFromCosmosDb.ShouldNotBeNull();
		aggregateFromCosmosDb.Id().ShouldBe(aggregate.Id());
		aggregateFromCosmosDb.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromCosmosDb.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromCosmosDb.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromCosmosDb.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);
		aggregateFromCosmosDb.Details.Etag.ShouldBe(aggregate.Details.Etag);
	}
}
