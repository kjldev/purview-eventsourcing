using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Test;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer;

public sealed class SqlServerSnapshotEventStoreTests
{
	[Fact]
	public void Constructor_GivenValidParameters_CreatesInstance()
	{
		// Arrange & Act
		using var store = CreateStore();

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public async Task CreateAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.CreateAsync("test-id");

		// Assert
		result.ShouldBe(expectedAggregate);
		await eventStore.Received(1).CreateAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetAsync(Arg.Any<string>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.GetAsync("test-id", null);

		// Assert
		result.ShouldBe(expectedAggregate);
		await eventStore.Received(1).GetAsync("test-id", null, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetOrCreateAsync_GivenAggregateId_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetOrCreateAsync(Arg.Any<string?>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.GetOrCreateAsync("test-id", null);

		// Assert
		result.ShouldBe(expectedAggregate);
		await eventStore.Received(1).GetOrCreateAsync("test-id", null, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetAtAsync_GivenAggregateIdAndVersion_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetAtAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.GetAtAsync("test-id", 5, null);

		// Assert
		result.ShouldBe(expectedAggregate);
		await eventStore.Received(1).GetAtAsync("test-id", 5, null, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task IsDeletedAsync_DelegatesToEventStore()
	{
		// Arrange
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.IsDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.IsDeletedAsync("test-id");

		// Assert
		result.ShouldBeTrue();
		await eventStore.Received(1).IsDeletedAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task GetDeletedAsync_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.GetDeletedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.GetDeletedAsync("test-id");

		// Assert
		result.ShouldBe(expectedAggregate);
		await eventStore.Received(1).GetDeletedAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ExistsAsync_DelegatesToEventStore()
	{
		// Arrange
		var expectedState = ExistsState.Exists;
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expectedState);

		using var store = CreateStore(eventStore);

		// Act
		var result = await store.ExistsAsync("test-id");

		// Assert
		result.ShouldBe(expectedState);
		await eventStore.Received(1).ExistsAsync("test-id", Arg.Any<CancellationToken>());
	}

	[Fact]
	public void FulfilRequirements_DelegatesToEventStore()
	{
		// Arrange
		var expectedAggregate = TestHelpers.Aggregate<TestAggregate>();
		var eventStore = Substitute.For<INonQueryableEventStore<TestAggregate>>();
		eventStore.FulfilRequirements(Arg.Any<TestAggregate>()).Returns(expectedAggregate);

		using var store = CreateStore(eventStore);

		// Act
		var result = store.FulfilRequirements(expectedAggregate);

		// Assert
		result.ShouldBe(expectedAggregate);
		eventStore.Received(1).FulfilRequirements(expectedAggregate);
	}

	[Fact]
	public void SqlServerEventStoreOptions_HasDefaultValues()
	{
		// Act
		var options = new SqlServerEventStoreOptions();

		// Assert
		options.TableName.ShouldBe("Snapshots");
		options.SchemaName.ShouldBe("dbo");
		options.AutoCreateTable.ShouldBeTrue();
	}

	[Fact]
	public void SqlServerEventStoreOptions_HasCorrectConfigSectionKey()
	{
		// Assert
		SqlServerEventStoreOptions.SqlServerEventStore.ShouldBe("EventStore:SqlServerSnapshot");
	}

	static SqlServerSnapshotEventStore<TestAggregate> CreateStore(INonQueryableEventStore<TestAggregate>? eventStore = null)
	{
		eventStore ??= Substitute.For<INonQueryableEventStore<TestAggregate>>();
		var options = CreateOptions();
		var telemetry = Substitute.For<ISqlServerSnapshotEventStoreTelemetry>();

		return new SqlServerSnapshotEventStore<TestAggregate>(eventStore, options, telemetry);
	}

	static IOptions<SqlServerEventStoreOptions> CreateOptions()
	{
		return Options.Create(new SqlServerEventStoreOptions
		{
			ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;",
			TableName = "TestSnapshots",
			SchemaName = "dbo",
			AutoCreateTable = false
		});
	}
}
