using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing.Aggregates.Snapshotting;

public sealed class SnapshotStrategyTests
{
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
	[Arguments(10, 5, true)]   // 10 % 5 == 0
	[Arguments(15, 5, true)]   // 15 % 5 == 0
	[Arguments(3, 5, false)]   // 3 % 5 != 0
	[Arguments(7, 5, false)]   // 7 % 5 != 0
	public async Task IntervalStrategy_GivenSavedVersionAndInterval_ReturnsExpected(
		int savedVersion,
		int interval,
		bool expectedResult,
		CancellationToken cancellationToken
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
	public async Task IntervalStrategy_GivenZeroEventsApplied_ReturnsFalse(CancellationToken cancellationToken)
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
	public async Task IntervalStrategy_GivenNullAggregate_ThrowsArgumentNullException(CancellationToken cancellationToken)
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
	public async Task IntervalStrategy_GivenIntervalLessThanOne_ThrowsArgumentOutOfRangeException(
		int interval,
		CancellationToken cancellationToken
	)
	{
		// Act & Assert
		await Assert.That(() => new IntervalSnapshotStrategy<TestAggregate>(interval))
			.Throws<ArgumentOutOfRangeException>();
	}

	#endregion

	#region AlwaysSnapshotStrategy

	[Test]
	[Arguments(1)]
	[Arguments(5)]
	[Arguments(100)]
	public async Task AlwaysStrategy_GivenEventsApplied_AlwaysReturnsTrue(
		int eventsApplied,
		CancellationToken cancellationToken
	)
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
	public async Task AlwaysStrategy_GivenZeroEventsApplied_ReturnsFalse(CancellationToken cancellationToken)
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
	public async Task NeverStrategy_GivenAnyInput_AlwaysReturnsFalse(
		int eventsApplied,
		CancellationToken cancellationToken
	)
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
}
