using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	[Arguments(10, 5)]
	[Arguments(20, 5)]
	[Arguments(25, 5)]
	[Arguments(26, 5)]
	[Arguments(27, 5)]
	[Arguments(50, 5)]
	[Arguments(51, 5)]
	public async Task ListAsync_GivenData_ListsAsExpected(int numberOfAggregates, int pageCount)
	{
		const int numberOfEvents = 10;

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
		List<PersistenceAggregate> aggregates = [];

		var aggregateResponse = await eventStore.ListAsync(
			maxRecordCount: pageCount,
			cancellationToken: tokenSource.Token
		);
		aggregates.AddRange(aggregateResponse.Results);

		while (aggregateResponse.ContinuationToken != null)
		{
			aggregateResponse = await eventStore.ListAsync(
				aggregateResponse.ToRequest(),
				cancellationToken: tokenSource.Token
			);
			aggregates.AddRange(aggregateResponse.Results);
		}

		// Assert
		await Assert.That(aggregates).HasCount(numberOfAggregates);
	}

	[Test]
	[Arguments(10, 5)]
	[Arguments(20, 5)]
	[Arguments(25, 5)]
	[Arguments(26, 5)]
	[Arguments(27, 5)]
	[Arguments(50, 5)]
	[Arguments(51, 5)]
	public async Task QueryAsync_GivenWhereClause_QueryAsExpected(int numberOfAggregates, int pageCount)
	{
		const int numberOfEvents = 10;

		// Arrange
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates * 2);
		var eventStore = context.EventStore;

		// These are matching.
		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");
			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(
				aggregate,
				cancellationToken: TestContext.Current.Execution.CancellationToken
			);

			await Assert.That(saveResult).IsTrue();
		}

		// These are non-matching.
		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex + (numberOfAggregates + 100000)}_{context.RunId}");

			// We're changing the event count so as to make the query not match these updated records.
			for (var eventIndex = 0; eventIndex < (numberOfEvents * 2); eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(
				aggregate,
				cancellationToken: TestContext.Current.Execution.CancellationToken
			);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		List<PersistenceAggregate> aggregates = [];

		Expression<Func<PersistenceAggregate, bool>> query = a => a.IncrementInt32 == numberOfEvents;

		var aggregateResponse = await eventStore.QueryAsync(
			query,
			maxRecordCount: pageCount,
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		aggregates.AddRange(aggregateResponse.Results);

		while (aggregateResponse.ContinuationToken != null)
		{
			aggregateResponse = await eventStore.QueryAsync(
				query,
				aggregateResponse.ToRequest(),
				cancellationToken: TestContext.Current.Execution.CancellationToken
			);
			aggregates.AddRange(aggregateResponse.Results);
		}

		// Assert
		await Assert.That(aggregates).HasCount(numberOfAggregates);
	}
}
