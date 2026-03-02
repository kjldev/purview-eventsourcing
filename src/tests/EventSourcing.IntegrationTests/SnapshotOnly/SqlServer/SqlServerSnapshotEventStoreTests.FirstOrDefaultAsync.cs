namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregatesHonoursDescendingOrder_ReturnsCorrectAggregate(CancellationToken cancellationToken)
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(i + 1);

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			orderByClause: m => m.OrderByDescending(p => p.Int32Value),
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.Int32Value).IsEqualTo(aggregateCount);
	}

	[Test]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregatesHonoursAscendingOrder_ReturnsCorrectAggregate(CancellationToken cancellationToken)
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			aggregate.SetInt32Value(i + 1);

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			orderByClause: m => m.OrderBy(p => p.Int32Value),
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Int32Value).IsEqualTo(1);
	}

	[Test]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregates_ShouldNotThrowException(CancellationToken cancellationToken)
	{
		const int aggregateCount = 10;
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		for (var i = 0; i < aggregateCount; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
			{
				aggregate.IncrementInt32Value();
			}

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		Func<Task> func = async () =>
			await context.EventStore.FirstOrDefaultAsync(
				m => m.IncrementInt32 == matchingIncrement,
				cancellationToken: cancellationToken
			);

		// Assert
		await Assert.That(func).ThrowsNothing();
	}

	[Test]
	public async Task FirstOrDefaultAsync_GivenMultipleMatchingAggregates_ShouldNotReturnNull(CancellationToken cancellationToken)
	{
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		for (var i = 0; i < 10; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNotNull();
	}

	[Test]
	public async Task FirstOrDefaultAsync_GivenSingleMatchingAggregate_ReturnsAggregate(CancellationToken cancellationToken)
	{
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		for (var x = 0; x < matchingIncrement; x++)
			aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(saveResult).IsTrue();

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == matchingIncrement,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNotNull();
		await Assert.That(result.Id()).IsEqualTo(aggregateId);
	}

	[Test]
	public async Task FirstOrDefaultAsync_GivenNoMatchingAggregates_ReturnsNull(CancellationToken cancellationToken)
	{
		const int matchingIncrement = 10;

		// Arrange
		var context = fixture.CreateContext();

		for (var i = 0; i < 10; i++)
		{
			var aggregate = CreateAggregate();
			for (var x = 0; x < matchingIncrement; x++)
				aggregate.IncrementInt32Value();

			bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

			await Assert.That(saveResult).IsTrue();
		}

		// Act
		var result = await context.EventStore.FirstOrDefaultAsync(
			m => m.IncrementInt32 == -1,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsNull();
	}
}
