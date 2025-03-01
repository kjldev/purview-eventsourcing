using Purview.EventSourcing.Aggregates.Test;
using Purview.EventSourcing.Aggregates.Test.Events;

namespace Purview.EventSourcing.Aggregates;

public partial class AggregateBaseTests
{
	[Fact]
	public void GetHashCode_GivenIdentificationEvents_GeneratesIdenticalHashCodes()
	{
		// Arrange
		static AppendToReadOnlyDictionaryEvent Generate()
		{
			AppendToReadOnlyDictionaryEvent @event = new()
			{
				Key = "a-key",
				Values = ["a-value", "another-value"]
			};

			return @event;
		}

		var event1 = Generate();
		var event2 = Generate();

		// Act
		var event1HashCode = event1.GetHashCode();
		var event2HashCode = event2.GetHashCode();

		// Assert
		event1HashCode.ShouldBe(event2HashCode);
	}

	[Fact]
	public void Register_GivenEventTypeNotEndingWithEvent_ThrowsInvalidOperationException()
	{
		// Arrange/ Act
		var act = () =>
		{
			InvalidEventTestAggregate aggregate = new();
		};

		// Assert
		var ex = act.ShouldThrow<InvalidOperationException>();
		ex.Message.ShouldContain(typeof(InvalidEventType).FullName!);
	}

	[Fact]
	public void RecordEvent_GivenEvent_AppliesEvent()
	{
		// Arrange
		var aggregate = CreateTestAggregate();

		// Act
		aggregate.RecordEvent();

		// Assert
		aggregate.EventRecorded.ShouldBeTrue();
	}

	[Fact]
	public void GetUnsavedEvents_GivenEvent_RecordsOneEventToBeRecorded()
	{
		// Arrange
		var aggregate = CreateTestAggregate();

		aggregate.RecordEvent();

		// Act
		var events = aggregate.GetUnsavedEvents();

		// Assert
		events.ShouldHaveSingleItem();
	}

	[Theory]
	[InlineData(10, 5, 5)]
	[InlineData(100, 50, 50)]
	[InlineData(100, 100, 0)]
	public void ClearUnsavedEvents_GivenEventsAndClearEventsCalledWithSpecificVersion_ClearEventsThatAreGreaterThanOrOrEqualToSpecifiedVersion(int eventsToCreate, int versionToClear, int expectedVersion)
	{
		// Arrange
		var aggregate = CreateTestAggregate();
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.Increment();

		// Act
		aggregate.ClearUnsavedEvents(versionToClear);

		// Assert
		aggregate.Details.CurrentVersion.ShouldBe(expectedVersion);
		aggregate.GetUnsavedEvents().ShouldHaveCount(expectedVersion);
	}

	[Fact]
	public void ClearUnsavedEvents_GivenEventsAndClearEventsCalledWithNull_ClearEventsAllEvents()
	{
		// Arrange
		var aggregate = CreateTestAggregate();

		aggregate.Increment();
		aggregate.Increment();

		// Act
		aggregate.ClearUnsavedEvents();

		// Assert
		aggregate.GetUnsavedEvents().ShouldBeEmpty();
	}

	[Theory]
	[InlineData(4, 8)]
	[InlineData(4, 100)]
	[InlineData(100, 101)]
	[InlineData(100, 1001)]
	public void ClearUnsavedEvents_GivenClearValueIsGreaterThanEventDetailsAggregateVersion_SetsUpToSavedVersion(int eventsToCreate, int versionToClear)
	{
		// Arrange
		var aggregate = CreateTestAggregate();
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.Increment();

		// Act
		aggregate.ClearUnsavedEvents(versionToClear);

		// Assert
		aggregate.Details.SavedVersion.ShouldBe(0);
	}

	[Theory]
	[InlineData(4, 8)]
	[InlineData(4, 100)]
	[InlineData(100, 101)]
	[InlineData(100, 1001)]
	public void ClearUnsavedEvents_GivenClearValueIsGreaterThanEventDetailsAggregateVersionAndValuesPreviousSaved_SetsCurrentVersionTo0(int eventsToCreate, int versionToClear)
	{
		// Arrange
		var aggregate = CreateTestAggregate();
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.Increment();

		// Act
		aggregate.ClearUnsavedEvents(versionToClear);

		// Assert
		aggregate.Details.CurrentVersion.ShouldBe(0);
		aggregate.HasUnsavedEvents().ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10)]
	[InlineData(100)]
	public void DetailsCurrentVersion_GivenEvents_IncrementsCurrentVersion(int eventCount)
	{
		// Arrange
		var aggregate = CreateTestAggregate();

		// Act
		for (var i = 0; i < eventCount; i++)
			aggregate.Increment();

		// Assert
		aggregate.Details.CurrentVersion.ShouldBe(eventCount);
	}

	static TestAggregate CreateTestAggregate(string? id = null)
	{
		TestAggregate aggregate = new()
		{
			Details =
			{
				Id = id ?? Guid.NewGuid().ToString()
			}
		};

		return aggregate;
	}
}
