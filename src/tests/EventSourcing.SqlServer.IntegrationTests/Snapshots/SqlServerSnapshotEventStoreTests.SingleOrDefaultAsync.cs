using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshots;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task SingleOrDefaultAsync_GivenMultipleMatchingAggregates_ThrowsException(
		CancellationToken cancellationToken
	)
	{
		const int matchingIncrement = 10;

		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		for (var i = 0; i < matchingIncrement; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			var saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult.ToBoolean()).IsTrue();
			await Assert.That(saveResult.Skipped).IsFalse();
		}

		// Act
		async Task Func() =>
			await store.SingleOrDefaultAsync(
				m => m.IncrementInt32 == matchingIncrement,
				cancellationToken: cancellationToken
			);

		// Assert
		await Assert
			.That(Func)
			.Throws<InvalidOperationException>()
			.WithMessage("Sequence contains more than one element", StringComparison.Ordinal);
	}

	[Test]
	public async Task SingleOrDefaultAsync_GivenSingleMatchingAggregate_ReturnsAggregate(
		CancellationToken cancellationToken
	)
	{
		const int matchingIncrement = 10;

		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		for (var x = 0; x < matchingIncrement; x++)
			aggregate.IncrementInt32Value();

		bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await Assert.That(saveResult).IsTrue();

		// Act
		var result = await store.SingleOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result?.Id()).IsEqualTo(aggregateId);
	}

	[Test]
	public async Task SingleOrDefaultAsync_GivenNoMatchingAggregates_ReturnsNull(CancellationToken cancellationToken)
	{
		const int aggregatesToCreate = 10;
		const int eventsToCreate = 10;

		// Arrange
		var store = fixture.CreateSnapshotStore<PersistenceAggregate>();
		for (var i = 0; i < aggregatesToCreate; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < eventsToCreate; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await store.SaveAsync(aggregate, cancellationToken: cancellationToken);
			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await store.SingleOrDefaultAsync(
			m => m.IncrementInt32 == -1,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNull();
	}
}
