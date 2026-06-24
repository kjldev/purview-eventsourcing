using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Snapshotting;
using Purview.EventSourcing.Aggregates.Test;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer.Snapshots;

public sealed class SqlServerSnapshotEventStoreTests
{
	[Test]
	public async Task Constructor_GivenValidParameters_CreatesInstance()
	{
		// Arrange & Act
		var store = CreateStore();

		// Assert
		await Assert.That(store).IsNotNull();
	}

	[Test]
	public async Task CreateAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.CreateAsync("test-id");

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).CreateAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.GetAsync(Arg.Any<string>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetAsync("test-id", null);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetAsync("test-id", null, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetOrCreateAsync_GivenAggregateId_DelegatesToEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.GetOrCreateAsync(Arg.Any<string?>(), Arg.Any<EventStoreOperationContext?>(), Arg.Is(cancellationToken))
			.Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetOrCreateAsync("test-id", null, cancellationToken);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetOrCreateAsync("test-id", null, cancellationToken);
	}

	[Test]
	public async Task GetAtAsync_GivenAggregateIdAndVersion_DelegatesToEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.GetAtAsync(
				Arg.Any<string>(),
				Arg.Any<int>(),
				Arg.Any<EventStoreOperationContext?>(),
				Arg.Is(cancellationToken)
			)
			.Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetAtAsync("test-id", 5, null, cancellationToken);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetAtAsync("test-id", 5, null, cancellationToken);
	}

	[Test]
	public async Task IsDeletedAsync_DelegatesToEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.IsDeletedAsync(Arg.Any<string>(), cancellationToken).Returns(true);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.IsDeletedAsync("test-id", cancellationToken);

		// Assert
		await Assert.That(result).IsTrue();
		await eventStore.Received(1).IsDeletedAsync("test-id", cancellationToken);
	}

	[Test]
	public async Task GetDeletedAsync_DelegatesToEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetDeletedAsync("test-id", cancellationToken);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetDeletedAsync("test-id", cancellationToken);
	}

	[Test]
	public async Task ExistsAsync_DelegatesToEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var expectedState = ExistsState.Exists;
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedState);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.ExistsAsync("test-id", cancellationToken);

		// Assert
		await Assert.That(result).IsEqualTo(expectedState);
		await eventStore.Received(1).ExistsAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task FulfilRequirements_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.FulfilRequirements(Arg.Any<TestAggregate>()).Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = store.FulfilRequirements(expectedAggregate);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		eventStore.Received(1).FulfilRequirements(expectedAggregate);
	}

	[Test]
	public async Task SaveAsync_GivenNeverSnapshotStrategy_DoesNotWriteSnapshot(CancellationToken cancellationToken)
	{
		var aggregate = TestHelpers.Aggregate<TestAggregate>(creator: a => a.RecordEvent(), clearEvents: false);
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(args => new SaveResult<TestAggregate>(
				args.Arg<TestAggregate>(),
				new FluentValidation.Results.ValidationResult(),
				saved: true,
				skipped: false
			));
		var store = CreateStore(eventStore, snapshotStrategy: new NeverSnapshotStrategy<TestAggregate>());

		var result = await store.SaveAsync(aggregate, null, cancellationToken);

		await Assert.That(result.Saved).IsTrue();
		await eventStore.Received(1).SaveAsync(aggregate, null, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task SaveAsync_GivenContextSnapshotStrategy_OverridesDefaultStrategy(
		CancellationToken cancellationToken
	)
	{
		var aggregate = TestHelpers.Aggregate<TestAggregate>(creator: a => a.RecordEvent(), clearEvents: false);
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(args => new SaveResult<TestAggregate>(
				args.Arg<TestAggregate>(),
				new FluentValidation.Results.ValidationResult(),
				saved: true,
				skipped: false
			));

		var store = CreateStore(eventStore, snapshotStrategy: new AlwaysSnapshotStrategy<TestAggregate>());
		var context = new EventStoreOperationContext().SetSnapshotStrategy(new NeverSnapshotStrategy<TestAggregate>());

		var result = await store.SaveAsync(aggregate, context, cancellationToken);

		await Assert.That(result.Saved).IsTrue();
		await eventStore.Received(1).SaveAsync(aggregate, context, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Constructor_GivenOptionsWithAggregateTableOverride_CreatesStoreWithoutThrowing()
	{
		// Arrange
		var options = CreateDefaultOptions();
		options.AggregateTableOverrides["Test"] = new SqlServerSnapshotAggregateTableOverride
		{
			SchemaName = "custom",
			TableName = "CustomSnapshots",
		};

		// Act & Assert — should not throw
		var store = CreateStore(options: options);
		await Assert.That(store).IsNotNull();
	}

	[Test]
	public async Task Constructor_GivenOptionsWithPartialAggregateTableOverride_FallsBackToGlobalDefaults()
	{
		// Arrange — only schema override, table should fall back to global default
		var options = CreateDefaultOptions();
		options.AggregateTableOverrides["Test"] = new SqlServerSnapshotAggregateTableOverride
		{
			SchemaName = "custom",
			// TableName not set → falls back to global "TestSnapshots"
		};

		// Act & Assert — should not throw
		var store = CreateStore(options: options);
		await Assert.That(store).IsNotNull();
	}

	static SqlServerSnapshotEventStore<TestAggregate> CreateStore(
		INonQueryableEventStore<TestAggregate>? eventStore = null,
		SqlServerSnapshotEventStoreOptions? options = null,
		ISnapshotStrategy<TestAggregate>? snapshotStrategy = null,
		ISnapshotStrategySelector? snapshotStrategySelector = null
	)
	{
		eventStore ??= Substitute.For<INonQueryableEventStore<TestAggregate>>();
		var wrappedOptions = Options.Create(options ?? CreateDefaultOptions());
		var telemetry = Substitute.For<ISqlServerSnapshotEventStoreTelemetry>();

		return new(eventStore, wrappedOptions, telemetry, snapshotStrategy, snapshotStrategySelector);
	}

	static SqlServerSnapshotEventStoreOptions CreateDefaultOptions() =>
		new()
		{
			ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;",
			TableName = "TestSnapshots",
			SchemaName = "dbo",
			AutoCreateTable = false,
		};
}
