using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
{
	[Test]
	[Arguments(1, 1)]
	[Arguments(1, 5)]
	[Arguments(1, 10)]
	[Arguments(5, 1)]
	[Arguments(5, 5)]
	[Arguments(5, 10)]
	[Arguments(10, 1)]
	[Arguments(10, 5)]
	[Arguments(10, 10)]
	public async Task QueryAsync_GivenAggregatesExist_QueriesAsExpected(int numberOfAggregates, int numberOfEvents)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		IEnumerable<PersistenceAggregate> aggregates = (
			await eventStore.QueryAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: tokenSource.Token)
		).Results;

		// Assert
		await Assert.That(aggregates).HasCount(numberOfAggregates);
	}
}
