using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing.Aggregates.Snapshotting;

public sealed class SnapshotStrategyTests
{
	sealed class RecordingSnapshotStrategy : ISnapshotStrategy<TestAggregate>
	{
		public int Calls { get; private set; }

		public bool Result { get; set; }

		public bool ShouldSnapshot(TestAggregate aggregate, int eventsApplied)
		{
			Calls++;
			return Result;
		}
	}

	static TestAggregate CreateAggregate(int savedVersion)
	{
		var aggregate = new TestAggregate { Details = { Id = Guid.NewGuid().ToString() } };
		aggregate.Details.SavedVersion = savedVersion;
		return aggregate;
	}

	#region IntervalSnapshotStrategy

	[Test]
	[Arguments(1, 1, true)]
	[Arguments(2, 1, true)]
	[Arguments(5, 5, true)]
	[Arguments(10, 5, true)] // 10 % 5 == 0
	[Arguments(15, 5, true)] // 15 % 5 == 0
	[Arguments(3, 5, false)] // 3 % 5 != 0
	[Arguments(7, 5, false)] // 7 % 5 != 0
	public async Task IntervalStrategy_GivenSavedVersionAndInterval_ReturnsExpected(
		int savedVersion,
		int interval,
		bool expectedResult
	)
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy<TestAggregate>(interval);
		var aggregate = CreateAggregate(savedVersion);

		// Act
		var result = strategy.ShouldSnapshot(aggregate, eventsApplied: 1);

		// Assert
		await Assert.That(result).IsEqualTo(expectedResult);
	}

	[Test]
	public async Task IntervalStrategy_GivenZeroEventsApplied_ReturnsFalse()
	{
		// Arrange — interval of 1 would normally always snapshot, but 0 events means nothing was saved
		var strategy = new IntervalSnapshotStrategy<TestAggregate>(1);
		var aggregate = CreateAggregate(10);

		// Act
		var result = strategy.ShouldSnapshot(aggregate, eventsApplied: 0);

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task IntervalStrategy_GivenNullAggregate_ThrowsArgumentNullException()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy<TestAggregate>(1);

		// Act & Assert
		await Assert.That(() => strategy.ShouldSnapshot(null!, eventsApplied: 1)).Throws<ArgumentNullException>();
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	[Arguments(-100)]
	public async Task IntervalStrategy_GivenIntervalLessThanOne_ThrowsArgumentOutOfRangeException(int interval)
	{
		// Act & Assert
		await Assert
			.That(() => new IntervalSnapshotStrategy<TestAggregate>(interval))
			.Throws<ArgumentOutOfRangeException>();
	}

	#endregion

	#region AlwaysSnapshotStrategy

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(100)]
	public async Task AlwaysStrategy_GivenEventsApplied_AlwaysReturnsTrue(int eventsApplied)
	{
		// Arrange
		var strategy = new AlwaysSnapshotStrategy<TestAggregate>();
		var aggregate = CreateAggregate(savedVersion: 1);

		// Act
		var result = strategy.ShouldSnapshot(aggregate, eventsApplied);

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task AlwaysStrategy_GivenZeroEventsApplied_ReturnsFalse()
	{
		// Arrange
		var strategy = new AlwaysSnapshotStrategy<TestAggregate>();
		var aggregate = CreateAggregate(savedVersion: 1);

		// Act
		var result = strategy.ShouldSnapshot(aggregate, eventsApplied: 0);

		// Assert
		await Assert.That(result).IsFalse();
	}

	#endregion

	#region NeverSnapshotStrategy

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(100)]
	public async Task NeverStrategy_GivenAnyInput_AlwaysReturnsFalse(int eventsApplied)
	{
		// Arrange
		var strategy = new NeverSnapshotStrategy<TestAggregate>();
		var aggregate = CreateAggregate(savedVersion: 1);

		// Act
		var result = strategy.ShouldSnapshot(aggregate, eventsApplied);

		// Assert
		await Assert.That(result).IsFalse();
	}

	#endregion

	#region SnapshotStrategySelector

	[Test]
	public async Task Selector_GivenAggregateOverride_ReturnsOverride()
	{
		var expected = new NeverSnapshotStrategy<TestAggregate>();
		var selector = new SnapshotStrategySelector()
			.SetDefault(new AlwaysSnapshotStrategy<TestAggregate>())
			.Set(expected);

		var selected = selector.Resolve<TestAggregate>();

		await Assert.That(selected).IsSameReferenceAs(expected);
	}

	[Test]
	public async Task Selector_GivenNoAggregateOverride_ReturnsDefault()
	{
		var expected = new NeverSnapshotStrategy<TestAggregate>();
		var selector = new SnapshotStrategySelector().SetDefault(expected);

		var selected = selector.Resolve<TestAggregate>();

		await Assert.That(selected).IsSameReferenceAs(expected);
	}

	#endregion

	#region SnapshotStrategyResolver

	[Test]
	public async Task Resolver_GivenContextAggregateStrategy_PrefersContext()
	{
		var defaultStrategy = new RecordingSnapshotStrategy { Result = false };
		var selectorStrategy = new RecordingSnapshotStrategy { Result = false };
		var contextStrategy = new RecordingSnapshotStrategy { Result = true };

		var context = new EventStoreOperationContext
		{
			SnapshotStrategySelector = new SnapshotStrategySelector().SetDefault(selectorStrategy),
		}.SetSnapshotStrategy(contextStrategy);

		var aggregate = CreateAggregate(savedVersion: 1);
		aggregate.Details.CurrentVersion = 2;
		var shouldSnapshot = SnapshotStrategyResolver.ShouldSnapshot(
			aggregate,
			eventsApplied: 1,
			context,
			defaultStrategy
		);

		await Assert.That(shouldSnapshot).IsTrue();
		await Assert.That(contextStrategy.Calls).IsEqualTo(1);
		await Assert.That(selectorStrategy.Calls).IsEqualTo(0);
		await Assert.That(defaultStrategy.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task Resolver_GivenContextSelectorAndNoContextAggregateStrategy_UsesContextSelector()
	{
		var defaultStrategy = new RecordingSnapshotStrategy { Result = false };
		var contextSelectorStrategy = new RecordingSnapshotStrategy { Result = true };
		var context = new EventStoreOperationContext
		{
			SnapshotStrategySelector = new SnapshotStrategySelector().SetDefault(contextSelectorStrategy),
		};

		var aggregate = CreateAggregate(savedVersion: 1);
		aggregate.Details.CurrentVersion = 2;
		var shouldSnapshot = SnapshotStrategyResolver.ShouldSnapshot(
			aggregate,
			eventsApplied: 1,
			context,
			defaultStrategy
		);

		await Assert.That(shouldSnapshot).IsTrue();
		await Assert.That(contextSelectorStrategy.Calls).IsEqualTo(1);
		await Assert.That(defaultStrategy.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task Resolver_GivenStoreSelectorAndNoContextSelector_UsesStoreSelector()
	{
		var defaultStrategy = new RecordingSnapshotStrategy { Result = false };
		var storeSelectorStrategy = new RecordingSnapshotStrategy { Result = true };
		var storeSelector = new SnapshotStrategySelector().SetDefault(storeSelectorStrategy);

		var aggregate = CreateAggregate(savedVersion: 1);
		aggregate.Details.CurrentVersion = 2;
		var shouldSnapshot = SnapshotStrategyResolver.ShouldSnapshot(
			aggregate,
			eventsApplied: 1,
			context: null,
			defaultStrategy,
			storeSelector
		);

		await Assert.That(shouldSnapshot).IsTrue();
		await Assert.That(storeSelectorStrategy.Calls).IsEqualTo(1);
		await Assert.That(defaultStrategy.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task Resolver_GivenNoContextOrSelectors_UsesDefault()
	{
		var defaultStrategy = new RecordingSnapshotStrategy { Result = true };
		var aggregate = CreateAggregate(savedVersion: 1);
		aggregate.Details.CurrentVersion = 2;

		var shouldSnapshot = SnapshotStrategyResolver.ShouldSnapshot(
			aggregate,
			eventsApplied: 1,
			context: null,
			defaultStrategy
		);

		await Assert.That(shouldSnapshot).IsTrue();
		await Assert.That(defaultStrategy.Calls).IsEqualTo(1);
	}

	#endregion
}
