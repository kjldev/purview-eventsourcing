using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates.Test;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer;

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
	public async Task GetOrCreateAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.GetOrCreateAsync(Arg.Any<string?>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetOrCreateAsync("test-id", null);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetOrCreateAsync("test-id", null, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAtAsync_GivenAggregateIdAndVersion_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore
			.GetAtAsync(
				Arg.Any<string>(),
				Arg.Any<int>(),
				Arg.Any<EventStoreOperationContext?>(),
				Arg.Any<CancellationToken>()
			)
			.Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetAtAsync("test-id", 5, null);

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetAtAsync("test-id", 5, null, Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task IsDeletedAsync_DelegatesToEventStore()
	{
		// Arrange
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.IsDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.IsDeletedAsync("test-id");

		// Assert
		await Assert.That(result).IsTrue();
		await eventStore.Received(1).IsDeletedAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetDeletedAsync_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.GetDeletedAsync("test-id");

		// Assert
		await Assert.That(result).IsEqualTo(expectedAggregate);
		await eventStore.Received(1).GetDeletedAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_DelegatesToEventStore()
	{
		// Arrange
		var expectedState = ExistsState.Exists;
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedState);

		var store = CreateStore(eventStore);

		// Act
		var result = await store.ExistsAsync("test-id");

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

	static SqlServerSnapshotEventStore<TestAggregate> CreateStore(
		INonQueryableEventStore<TestAggregate>? eventStore = null,
		SqlServerSnapshotEventStoreOptions? options = null
	)
	{
		eventStore ??= Substitute.For<INonQueryableEventStore<TestAggregate>>();
		var wrappedOptions = Options.Create(options ?? CreateDefaultOptions());
		var telemetry = Substitute.For<ISqlServerSnapshotEventStoreTelemetry>();

		return new(eventStore, wrappedOptions, telemetry);
	}

	static IOptions<SqlServerSnapshotEventStoreOptions> CreateOptions() => Options.Create(CreateDefaultOptions());

	static SqlServerSnapshotEventStoreOptions CreateDefaultOptions() =>
		new()
		{
			ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;",
			TableName = "TestSnapshots",
			SchemaName = "dbo",
			AutoCreateTable = false,
		};

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
}
