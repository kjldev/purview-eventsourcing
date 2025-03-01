namespace Purview.EventSourcing;

partial class AggregateEventNameMapperTests
{
	[Fact]
	public void GetName_GivenEventTypeEndingWithEvent_ReturnsMappedName()
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();
		var eventType = typeof(EventTypeEndingInEvent);

		// Act
		var result = mapper.GetName<CorrectlyNamedAggregate>(eventType);

		// Assert
		result.ShouldBe($"{CorrectlyNamedAggregateName}.event-type-ending-in");
	}

	[Fact]
	public void GetName_GivenEventTypeNotEndingInEvent_ReturnsTypeName()
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();
		var eventType = typeof(EventTypeNotEndingInEvent2);

		// Act
		var result = mapper.GetName<CorrectlyNamedAggregate>(eventType);

		// Assert
		result.ShouldBe(typeof(EventTypeNotEndingInEvent2).FullName);
	}
}
