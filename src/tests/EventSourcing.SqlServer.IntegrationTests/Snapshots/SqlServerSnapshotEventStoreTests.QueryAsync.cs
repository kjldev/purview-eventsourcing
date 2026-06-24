using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshots;

partial class SqlServerSnapshotEventStoreTests
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
	public async Task QueryAsync_GivenAggregatesExist_QueriesAsExpected(
		int numberOfAggregates,
		int numberOfEvents,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"agg_{aggregateIndex}");

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregates = (
			await store.QueryAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: cancellationToken)
		).Results;

		// Assert
		await Assert.That(aggregates.Length).IsEqualTo(numberOfAggregates);
	}

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(10)]
	public async Task QueryAsync_GivenAggregateType_QueriesAsExpected(
		int numberOfAggregates,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		var aggregateType = CreateAggregate().AggregateType;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"agg_{aggregateIndex}");
			aggregate.IncrementInt32Value();

			bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregates = (
			await store.QueryAsync(
				m => m.AggregateType == aggregateType,
				maxRecordCount: numberOfAggregates + 1,
				cancellationToken: cancellationToken
			)
		).Results;

		// Assert
		await Assert.That(aggregates.Length).IsEqualTo(numberOfAggregates);
	}
}
