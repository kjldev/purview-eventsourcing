using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Theory]
	[InlineData(1, 1)]
	[InlineData(1, 5)]
	[InlineData(1, 10)]
	[InlineData(5, 1)]
	[InlineData(5, 5)]
	[InlineData(5, 10)]
	[InlineData(10, 1)]
	[InlineData(10, 5)]
	[InlineData(10, 10)]
	public async Task QueryAsync_GivenAggregatesExist_QueriesAsExpected(int numberOfAggregates, int numberOfEvents)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		IEnumerable<PersistenceAggregate> aggregates = (await eventStore.QueryAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: tokenSource.Token)).Results;

		// Assert
		aggregates.ShouldHaveCount(numberOfAggregates);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public async Task QueryAsync_GivenAggregateType_QueriesAsExpected(int numberOfAggregates)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var aggregateType = CreateAggregate().AggregateType;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");
			aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var aggregates = (await context.EventStore.QueryAsync(m => m.AggregateType == aggregateType, maxRecordCount: numberOfAggregates + 1, cancellationToken: tokenSource.Token)).Results;

		// Assert
		aggregates.ShouldHaveCount(numberOfAggregates);
	}
}
