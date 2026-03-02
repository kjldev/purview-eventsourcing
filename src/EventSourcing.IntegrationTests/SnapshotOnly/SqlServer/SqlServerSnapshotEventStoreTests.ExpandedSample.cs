using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(10)]
	[Arguments(25)]
	public async Task CountAsync_GivenAggregatesExist_ReturnsCorrectCount(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		var count = await eventStore.CountAsync(
			m => m.IncrementInt32 == numberOfEvents,
			cancellationToken: tokenSource.Token
		);

		// Assert
		await Assert.That(count).IsEqualTo(numberOfAggregates);
	}

	[Test]
	public async Task CountAsync_GivenNoMatchingAggregates_ReturnsZero()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext();

		var aggregate = CreateAggregate();
		aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(saveResult).IsTrue();

		// Act
		var count = await context.EventStore.CountAsync(
			m => m.IncrementInt32 == -1,
			cancellationToken: tokenSource.Token
		);

		// Assert
		await Assert.That(count).IsEqualTo(0);
	}

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(10)]
	public async Task GetQueryEnumerableAsync_GivenAggregatesExist_EnumeratesAsExpected(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		await foreach (
			var aggregate in eventStore.GetQueryEnumerableAsync(
				m => m.IncrementInt32 == numberOfEvents,
				cancellationToken: tokenSource.Token
			)
		)
			aggregates.Add(aggregate);

		// Assert
		await Assert.That(aggregates).HasCount(numberOfAggregates);
	}

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(10)]
	public async Task GetListEnumerableAsync_GivenAggregatesExist_EnumeratesAsExpected(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		await foreach (var aggregate in eventStore.GetListEnumerableAsync(cancellationToken: tokenSource.Token))
			aggregates.Add(aggregate);

		// Assert
		await Assert.That(aggregates).HasCount(numberOfAggregates);
	}

	[Test]
	public async Task SaveAsync_GivenAggregateWithComplexProperties_PersistsCorrectly()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);

		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		aggregate.SetInt32Value(42);
		aggregate.AppendString("hello-");
		aggregate.AppendString("world");
		aggregate.SetComplexProperty(
			new Aggregates.ComplexTestType
			{
				Int16Property = 16,
				Int32Property = 32,
				Int64Property = 64,
				StringProperty = "complex-test",
				DateTimeOffsetProperty = DateTimeOffset.UtcNow,
			}
		);

		// Act
		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(saveResult).IsTrue();

		// Verify via direct SQL Server read
		var fromDb = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(fromDb).IsNotNull();
		await Assert.That(fromDb.IncrementInt32).IsEqualTo(3);
		await Assert.That(fromDb.Int32Value).IsEqualTo(42);
		await Assert.That(fromDb.StringProperty).IsEqualTo("hello-world");
		await Assert.That(fromDb.ComplexTestType).IsNotNull();
		await Assert.That(fromDb.ComplexTestType!.Int16Property).IsEqualTo((short)16);
		await Assert.That(fromDb.ComplexTestType.Int32Property).IsEqualTo(32);
		await Assert.That(fromDb.ComplexTestType.Int64Property).IsEqualTo(64);
		await Assert.That(fromDb.ComplexTestType.StringProperty).IsEqualTo("complex-test");

		// Also verify via LINQ query
		var queried = await context.EventStore.SingleOrDefaultAsync(
			m => m.Int32Value == 42,
			cancellationToken: tokenSource.Token
		);
		await Assert.That(queried).IsNotNull();
		await Assert.That(queried!.Id()).IsEqualTo(aggregateId);
		await Assert.That(queried.ComplexTestType).IsNotNull();
		await Assert.That(queried.ComplexTestType!.StringProperty).IsEqualTo("complex-test");
	}

	[Test]
	public async Task SaveAsync_GivenMultipleSavesOfSameAggregate_UpdatesSnapshot()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		bool firstSave = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(firstSave).IsTrue();

		var firstVersion = aggregate.Details.CurrentVersion;

		// Modify and save again
		aggregate.IncrementInt32Value();
		aggregate.SetInt32Value(99);

		bool secondSave = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await Assert.That(secondSave).IsTrue();

		// Act - Read from SQL Server
		var fromDb = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(
			aggregateId,
			cancellationToken: tokenSource.Token
		);

		// Assert - Should have the latest state
		await Assert.That(fromDb).IsNotNull();
		await Assert.That(fromDb.IncrementInt32).IsEqualTo(2);
		await Assert.That(fromDb.Int32Value).IsEqualTo(99);
		await Assert.That(fromDb.Details.CurrentVersion).IsGreaterThan(firstVersion);
	}
}
