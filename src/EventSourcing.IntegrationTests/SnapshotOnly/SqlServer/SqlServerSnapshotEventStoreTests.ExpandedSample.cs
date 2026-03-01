using Purview.EventSourcing.Aggregates.Persistence;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStoreTests
{
	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(25)]
	public async Task CountAsync_GivenAggregatesExist_ReturnsCorrectCount(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		var count = await eventStore.CountAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: tokenSource.Token);

		// Assert
		count.ShouldBe(numberOfAggregates);
	}

	[Fact]
	public async Task CountAsync_GivenNoMatchingAggregates_ReturnsZero()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregate = CreateAggregate();
		aggregate.IncrementInt32Value();

		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		saveResult.ShouldBeTrue();

		// Act
		var count = await context.EventStore.CountAsync(m => m.IncrementInt32 == -1, cancellationToken: tokenSource.Token);

		// Assert
		count.ShouldBe(0);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public async Task GetQueryEnumerableAsync_GivenAggregatesExist_EnumeratesAsExpected(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		List<PersistenceAggregate> aggregates = [];
		await foreach (var aggregate in eventStore.GetQueryEnumerableAsync(m => m.IncrementInt32 == numberOfEvents, cancellationToken: tokenSource.Token))
			aggregates.Add(aggregate);

		// Assert
		aggregates.ShouldHaveCount(numberOfAggregates);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public async Task GetListEnumerableAsync_GivenAggregatesExist_EnumeratesAsExpected(int numberOfAggregates)
	{
		const int numberOfEvents = 5;

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
		List<PersistenceAggregate> aggregates = [];
		await foreach (var aggregate in eventStore.GetListEnumerableAsync(cancellationToken: tokenSource.Token))
			aggregates.Add(aggregate);

		// Assert
		aggregates.ShouldHaveCount(numberOfAggregates);
	}

	[Fact]
	public async Task SaveAsync_GivenAggregateWithComplexProperties_PersistsCorrectly()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);

		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		aggregate.IncrementInt32Value();
		aggregate.SetInt32Value(42);
		aggregate.AppendString("hello-");
		aggregate.AppendString("world");
		aggregate.SetComplexProperty(new Aggregates.ComplexTestType
		{
			Int16Property = 16,
			Int32Property = 32,
			Int64Property = 64,
			StringProperty = "complex-test",
			DateTimeOffsetProperty = DateTimeOffset.UtcNow
		});

		// Act
		bool saveResult = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		saveResult.ShouldBeTrue();

		// Verify via direct SQL Server read
		var fromDb = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);
		fromDb.ShouldNotBeNull();
		fromDb.IncrementInt32.ShouldBe(3);
		fromDb.Int32Value.ShouldBe(42);
		fromDb.StringProperty.ShouldBe("hello-world");
		fromDb.ComplexTestType.ShouldNotBeNull();
		fromDb.ComplexTestType!.Int16Property.ShouldBe((short)16);
		fromDb.ComplexTestType.Int32Property.ShouldBe(32);
		fromDb.ComplexTestType.Int64Property.ShouldBe(64);
		fromDb.ComplexTestType.StringProperty.ShouldBe("complex-test");

		// Also verify via LINQ query
		var queried = await context.EventStore.SingleOrDefaultAsync(m => m.Int32Value == 42, cancellationToken: tokenSource.Token);
		queried.ShouldNotBeNull();
		queried!.Id().ShouldBe(aggregateId);
		queried.ComplexTestType.ShouldNotBeNull();
		queried.ComplexTestType!.StringProperty.ShouldBe("complex-test");
	}

	[Fact]
	public async Task SaveAsync_GivenMultipleSavesOfSameAggregate_UpdatesSnapshot()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var context = fixture.CreateContext();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregate = CreateAggregate(id: aggregateId);
		aggregate.IncrementInt32Value();

		bool firstSave = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		firstSave.ShouldBeTrue();

		var firstVersion = aggregate.Details.CurrentVersion;

		// Modify and save again
		aggregate.IncrementInt32Value();
		aggregate.SetInt32Value(99);

		bool secondSave = await context.EventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		secondSave.ShouldBeTrue();

		// Act - Read from SQL Server
		var fromDb = await context.SqlServerClient.GetByIdAsync<PersistenceAggregate>(aggregateId, cancellationToken: tokenSource.Token);

		// Assert - Should have the latest state
		fromDb.ShouldNotBeNull();
		fromDb.IncrementInt32.ShouldBe(2);
		fromDb.Int32Value.ShouldBe(99);
		fromDb.Details.CurrentVersion.ShouldBeGreaterThan(firstVersion);
	}
}
