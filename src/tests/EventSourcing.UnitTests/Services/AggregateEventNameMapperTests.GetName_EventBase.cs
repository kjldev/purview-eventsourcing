namespace Purview.EventSourcing;

partial class AggregateEventNameMapperTests
{
    [Test]
    public async Task GetName_GivenEventInstanceWithDefinedName_ReturnsFullTypeName()
    {
        // Arrange
        var mapper = CreateMapper<CorrectlyNamedAggregate>();
        EventTypeEndingInEvent @event = new();

        // Act
        var result = mapper.GetName<CorrectlyNamedAggregate>(@event);

        // Assert
        await Assert.That(result).IsEqualTo($"{CorrectlyNamedAggregateName}.event-type-ending-in");
    }

    [Test]
    public async Task GetName_GivenEventInstanceWithNoDefinedName_ReturnsFullTypeName()
    {
        // Arrange
        var mapper = CreateMapper<CorrectlyNamedAggregate>();
        EventTypeNotEndingInEvent2 @event = new();

        // Act
        var result = mapper.GetName<CorrectlyNamedAggregate>(@event);

        // Assert
        await Assert.That(result).IsEqualTo(typeof(EventTypeNotEndingInEvent2).FullName);
    }

    [Test]
    [Arguments(
        "Purview.Services.UserProfile.Aggregates.UserProfile.Events.ClearProfileAttributesEvent, EventSourcing.UnitTests",
        "clear-profile-attributes"
    )]
    [Arguments(
        "Purview.Services.UserProfile.Aggregates.UserProfile.Events.ClearRolesEvent, EventSourcing.UnitTests",
        "clear-roles"
    )]
    public async Task GetName_GivenEventName_MatchesExpectation(
        string eventType,
        string expectation
    )
    {
        // Arrange
        var mapper = CreateMapper<CorrectlyNamedAggregate>();

        // Act
        var aggregateEventType = Type.GetType(eventType, true);
        await Assert.That(aggregateEventType).IsNotNull();

        var result = mapper.GetName<CorrectlyNamedAggregate>(aggregateEventType!);

        // Assert
        await Assert.That(result).IsEqualTo($"{CorrectlyNamedAggregateName}.{expectation}");
    }
}
