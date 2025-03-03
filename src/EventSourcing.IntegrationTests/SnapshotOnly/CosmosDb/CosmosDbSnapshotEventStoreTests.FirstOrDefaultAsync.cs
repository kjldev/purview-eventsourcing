namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStoreTests
{
	[Fact]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregatesHonoursDescendingOrder_ReturnsCorrectAggregate()
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(i + 1);

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			orderByClause: m => m.OrderByDescending(p => p.Int32Value),
			cancellationToken: tokenSource.Token
		);

		// Assert
		result.ShouldNotBeNull();
		result!.Int32Value.ShouldBe(aggregateCount);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregatesHonoursAscendingOrder_ReturnsCorrectAggregate()
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(i + 1);

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == matchingIncrement,
			   orderByClause: m => m.OrderBy(p => p.Int32Value),
			   cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldNotBeNull();
		result.Int32Value.ShouldBe(1);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregates_ShouldNotThrowException()
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
			{
				aggregate.IncrementInt32Value();
			}

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		Func<Task> func = async () => await context.EventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == matchingIncrement, cancellationToken: tokenSource.Token);

		// Assert
		await Should.NotThrowAsync(func);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregates_ShouldNotReturnNull()
	{
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		for (var i = 0; i < 10; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == matchingIncrement, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task FirstOrDefaultAsync_GivenSingleMatchingAggregates_ReturnsAggregate()
	{
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		for (var x = 0; x < matchingIncrement; x++)
			aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		saveResult.ShouldBeTrue();

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == matchingIncrement, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldNotBeNull();
		result.Id().ShouldBe(aggregateId);
	}

	[Fact]
	public async Task FirstOrDefaultAsync_GivenNoMatchingAggregates_ReturnsNull()
	{
		const int matchingIncrement = 10;

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		await using var context = fixture.CreateContext();

		for (var i = 0; i < 10; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

			saveResult.ShouldBeTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(m => m.IncrementInt32 == -1, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeNull();
	}
}
