namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Test]
	public async Task SingleOrDefaultAsync_GivenMultipleMatchingAggregates_ThrowsException(
		CancellationToken cancellationToken
	)
	{
		const int matchingIncrement = 10;

		// Arrange
		await using var context = fixture.CreateContext();

		for (var i = 0; i < matchingIncrement; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			var saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult.ToBoolean()).IsTrue();
			await Assert.That(saveResult.Skipped).IsFalse();
		}

		// Act
		Func<Task> func = async () =>
			await context.EventStore.SingleOrDefaultAsync(
				m => m.IncrementInt32 == matchingIncrement,
				cancellationToken: cancellationToken
			);

		// Assert
		var ex = await Assert.That(func).Throws<InvalidOperationException>();
		await Assert.That(ex!.Message).IsEqualTo("Sequence contains more than one element");
	}

	[Test]
	public async Task SingleOrDefaultAsync_GivenSingleMatchingAggregates_ReturnsAggregate(
		CancellationToken cancellationToken
	)
	{
		const int matchingIncrement = 10;

		// Arrange
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		for (var x = 0; x < matchingIncrement; x++)
			aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await Assert.That(saveResult).IsTrue();

		// Act
		var result = await context.EventStore.SingleOrDefaultAsync(
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
		await using var context = fixture.CreateContext();

		for (var i = 0; i < aggregatesToCreate; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < eventsToCreate; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await context.EventStore.SingleOrDefaultAsync(
			m => m.IncrementInt32 == -1,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNull();
	}
}
