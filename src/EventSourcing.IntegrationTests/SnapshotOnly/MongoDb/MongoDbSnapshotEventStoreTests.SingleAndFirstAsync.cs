namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStoreTests
{
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(15)]
	[InlineData(50)]
	public async Task SingleOrDefaultAsync_GivenAggregatesExist_ReturnsAggregate(int numberOfAggregates)
	{
		string? firstId = null;

		const int numberOfEvents = 5;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");
			aggregate.AppendString(aggregate.Id());

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var aggregateResult = await eventStore.SingleOrDefaultAsync(m => m.StringProperty == firstId, cancellationToken: tokenSource.Token);

		// Assert
		aggregateResult.ShouldNotBeNull();
		aggregateResult.ShouldBeEquivalentTo(aggregateResult);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(15)]
	[InlineData(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExist_ReturnsAggregate(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: tokenSource.Token);

		// Assert
		aggregateResult.ShouldNotBeNull();
		aggregateResult.ShouldBeEquivalentTo(aggregateResult);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(15)]
	[InlineData(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExistWithDescendingOrdering_ReturnsCorrect(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(aggregateIndex + 1);

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == numberOfEvents, m => m.OrderByDescending(m => m.Int32Value), cancellationToken: tokenSource.Token);

		// Assert
		aggregateResult.ShouldNotBeNull();
		aggregateResult.Int32Value.ShouldBe(numberOfAggregates);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(15)]
	[InlineData(50)]
	public async Task FirstOrDefaultAsync_GivenAggregatesExistWithAscendingOrdering_ReturnsCorrect(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

		string? firstId = null;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext(correlationIdsToGenerate: numberOfAggregates);

		var eventStore = context.EventStore;

		for (var aggregateIndex = 0; aggregateIndex < numberOfAggregates; aggregateIndex++)
		{
			var aggregate = CreateAggregate($"{aggregateIndex}_{context.RunId}");

			firstId ??= aggregate.Id();

			for (var eventIndex = 0; eventIndex < numberOfEvents; eventIndex++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(aggregateIndex + 1);

			bool saveResult = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var aggregateResult = await eventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == numberOfEvents, m => m.OrderBy(m => m.Int32Value), cancellationToken: tokenSource.Token);

		// Assert
		aggregateResult.ShouldNotBeNull();
		aggregateResult.Int32Value.ShouldBe(1);
	}
}

