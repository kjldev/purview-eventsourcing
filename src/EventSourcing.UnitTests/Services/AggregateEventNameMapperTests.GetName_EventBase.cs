namespace Purview.EventSourcing;

partial class AggregateEventNameMapperTests
{
	[Fact]
	public void GetName_GivenEventInstanceWithDefinedName_ReturnsFullTypeName()
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();
		EventTypeEndingInEvent @event = new();

		// Act
		var result = mapper.GetName<CorrectlyNamedAggregate>(@event);

		// Assert
		result.ShouldBe($"{CorrectlyNamedAggregateName}.event-type-ending-in");
	}

	[Fact]
	public void GetName_GivenEventInstanceWithNoDefinedName_ReturnsFullTypeName()
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();
		EventTypeNotEndingInEvent2 @event = new();

		// Act
		var result = mapper.GetName<CorrectlyNamedAggregate>(@event);

		// Assert
		result.ShouldBe(typeof(EventTypeNotEndingInEvent2).FullName);
	}

	[Theory]
	[InlineData("Purview.Services.UserProfile.Aggregates.UserProfile.Events.ClearProfileAttributesEvent, EventSourcing.UnitTests", "clear-profile-attributes")]
	[InlineData("Purview.Services.UserProfile.Aggregates.UserProfile.Events.ClearRolesEvent, EventSourcing.UnitTests", "clear-roles")]
	public void GetName_GivenEventName_MatchesExpectation(string eventType, string expectation)
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();

		// Act
		var aggregateEventType = Type.GetType(eventType, true);
		aggregateEventType.ShouldNotBeNull();

		var result = mapper.GetName<CorrectlyNamedAggregate>(aggregateEventType!);

		// Assert
		result.ShouldBe($"{CorrectlyNamedAggregateName}.{expectation}");
	}
}
