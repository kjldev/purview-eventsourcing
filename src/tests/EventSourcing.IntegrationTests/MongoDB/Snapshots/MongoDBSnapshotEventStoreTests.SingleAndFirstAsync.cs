namespace Purview.EventSourcing.MongoDB.Snapshots;

partial class MongoDBSnapshotEventStoreTests
{
	[Test]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(5)]
	[Arguments(10)]
	[Arguments(15)]
	[Arguments(50)]
	public async Task SingleOrDefaultAsync_GivenAggregatesExist_ReturnsAggregate(
		int numberOfAggregates,
		CancellationToken cancellationToken
	)
	{
		string? firstId = null;

		const int numberOfEvents = 5;

		// Arrange
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");
			aggregate.AppendString(aggregate.Id());

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregateResult = await eventStore.SingleOrDefaultAsync(
			m => m.StringProperty == firstId,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult).IsEquivalentTo(aggregateResult);
	}

	[Test]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(5)]
	[Arguments(10)]
	[Arguments(15)]
	[Arguments(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExist_ReturnsAggregate(
		int numberOfAggregates,
		CancellationToken cancellationToken
	)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == numberOfEvents,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult).IsEquivalentTo(aggregateResult);
	}

	[Test]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(5)]
	[Arguments(10)]
	[Arguments(15)]
	[Arguments(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExistWithDescendingOrdering_ReturnsCorrect(
		int numberOfAggregates,
		CancellationToken cancellationToken
	)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(aggregateIndex + 1);

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == numberOfEvents,
			m => m.OrderByDescending(m => m.Int32Value),
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult.Int32Value).IsEqualTo(numberOfAggregates);
	}

	[Test]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(5)]
	[Arguments(10)]
	[Arguments(15)]
	[Arguments(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExistWithAscendingOrdering_ReturnsCorrect(
		int numberOfAggregates,
		CancellationToken cancellationToken
	)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(aggregateIndex + 1);

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == numberOfEvents,
			m => m.OrderBy(m => m.Int32Value),
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(aggregateResult).IsNotNull();
		await Assert.That(aggregateResult.Int32Value).IsEqualTo(1);
	}
}
