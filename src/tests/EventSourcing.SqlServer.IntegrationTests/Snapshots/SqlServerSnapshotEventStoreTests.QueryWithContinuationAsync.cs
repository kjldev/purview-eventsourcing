using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshots;

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
	public async Task ListAsync_GivenData_ListsAsExpected(
		int numberOfAggregates,
		int pageCount,
		CancellationToken cancellationToken
	)
	{
		const int numberOfEvents = 10;

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
		List<PersistenceAggregate> aggregates = [];

		var aggregateResponse = await store.ListAsync(maxRecordCount: pageCount, cancellationToken: cancellationToken);
		aggregates.AddRange(aggregateResponse.Results);

		while (aggregateResponse.ContinuationToken != null)
		{
			aggregateResponse = await store.ListAsync(
				aggregateResponse.ToRequest(),
				cancellationToken: cancellationToken
			);
			aggregates.AddRange(aggregateResponse.Results);
		}

		// Assert
		await Assert.That(aggregates.Count).IsEqualTo(numberOfAggregates);
	}

	[Test]
	[Arguments(10, 5)]
	[Arguments(20, 5)]
	[Arguments(25, 5)]
	[Arguments(26, 5)]
	[Arguments(27, 5)]
	[Arguments(50, 5)]
	[Arguments(51, 5)]
	public async Task QueryAsync_GivenWhereClause_QueryAsExpected(
		int numberOfAggregates,
		int pageCount,
		CancellationToken cancellationToken
	)
	{
		const int numberOfEvents = 10;

		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		// These are matching.
		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"agg_{aggregateIndex}");
			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// These are non-matching.
		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"agg_{aggregateIndex + (numberOfAggregates + 100000)}");

			// We're changing the event count so as to make the query not match these updated records.
			for (var eventIndex = 0; eventIndex < (numberOfEvents * 2); eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		List<PersistenceAggregate> aggregates = [];

		Expression<Func<PersistenceAggregate, bool>> query = a => a.IncrementInt32 == numberOfEvents;

		var aggregateResponse = await store.QueryAsync(
			query,
			maxRecordCount: pageCount,
			cancellationToken: cancellationToken
		);
		aggregates.AddRange(aggregateResponse.Results);

		while (aggregateResponse.ContinuationToken != null)
		{
			aggregateResponse = await store.QueryAsync(
				query,
				aggregateResponse.ToRequest(),
				cancellationToken: cancellationToken
			);
			aggregates.AddRange(aggregateResponse.Results);
		}

		// Assert
		await Assert.That(aggregates.Count).IsEqualTo(numberOfAggregates);
	}
}
