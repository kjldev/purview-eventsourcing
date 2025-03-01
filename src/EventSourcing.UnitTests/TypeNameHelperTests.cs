using NSubstitute.ReturnsExtensions;

namespace Purview.EventSourcing;

public class TypeNameHelperTests
{
	[Theory]
	[InlineData("test", "TESTAggregate", "Aggregate")]
	[InlineData("test", "TestAggregate", "Aggregate")]
	[InlineData("test", "testAggregate", "Aggregate")]
	[InlineData("kieron", "KieronAggregate", "Aggregate")]
	[InlineData("kieron", "KIERONAggregate", "Aggregate")]
	[InlineData("kieron", "kieronAggregate", "Aggregate")]
	[InlineData("test", "TESTEvent", "Event")]
	[InlineData("test", "TestEvent", "Event")]
	[InlineData("test", "testEvent", "Event")]
	[InlineData("kieron", "KieronEvent", "Event")]
	[InlineData("kieron", "KIERONEvent", "Event")]
	[InlineData("kieron", "kieronEvent", "Event")]
	public void GetName_GivenAggregateTypeEndsWithTrimNamePart_ReturnsLoweredTypeNameWithoutTrimNamePart(string expectation, string aggregateName, string trimNamePart)
	{
		// Arrange
		var type = Substitute.For<Type>();
		type.Name.Returns(aggregateName);
		type.FullName.ReturnsNull();

		// Act
		var result = TypeNameHelper.GetName(type, trimNamePart);

		// Assert
		result.ShouldBe(expectation);
	}

	[Theory]
	[InlineData("test-kieron", "TestKieronAggregate", "Aggregate")]
	[InlineData("kieron-test", "KieronTestAggregate", "Aggregate")]
	[InlineData("kieron-test", "KieronTESTAggregate", "Aggregate")]
	[InlineData("learn-html-test", "LearnHTMLTestAggregate", "Aggregate")]
	[InlineData("test-kieron", "TestKieronEvent", "Event")]
	[InlineData("kieron-test", "KieronTestEvent", "Event")]
	[InlineData("kieron-test", "KieronTESTEvent", "Event")]
	[InlineData("learn-html-test", "LearnHTMLTestEvent", "Event")]
	public void GetName_GivenAggregateTypeEndsWithTrimNamePartAndHasTitleCasedName_ReturnsLoweredTypeNameWithoutAggregateSplitByDash(string expectation, string aggregateName, string trimNamePart)
	{
		// Arrange
		var type = Substitute.For<Type>();
		type.Name.Returns(aggregateName);
		type.FullName.ReturnsNull();

		// Act
		var result = TypeNameHelper.GetName(type, trimNamePart);

		// Assert
		result.ShouldBe(expectation);
	}

	[Theory]
	[InlineData("TEST", "TEST", "Aggregate")]
	[InlineData("Test", "Test", "Aggregate")]
	[InlineData("test", "test", "Aggregate")]
	[InlineData("Kieron", "Kieron", "Aggregate")]
	[InlineData("KIERON", "KIERON", "Aggregate")]
	[InlineData("kieron", "kieron", "Aggregate")]
	public void GetName_GivenTypeDoesNotEndsWithTrimNamePartAndFallThroughToFullTypeNameIsTrue_ReturnsTypeFullName(string expectation, string aggregateName, string trimNamePart)
	{
		// Arrange
		var type = Substitute.For<Type>();
		type.Name.Returns("anything");
		type.FullName.Returns(aggregateName);

		// Act
		var result = TypeNameHelper.GetName(type, trimNamePart, fallThroughToFullTypeName: true);

		// Assert
		result.ShouldBe(expectation);
	}


	[Theory]
	[InlineData("TEST", "TEST", "Aggregate")]
	[InlineData("Test", "Test", "Aggregate")]
	[InlineData("test", "test", "Aggregate")]
	[InlineData("Kieron", "Kieron", "Aggregate")]
	[InlineData("KIERON", "KIERON", "Aggregate")]
	[InlineData("kieron", "kieron", "Aggregate")]
	public void GetName_GivenTypeDoesNotEndsWithTrimNamePart_ReturnsTypeName(string expectation, string aggregateName, string trimNamePart)
	{
		// Arrange
		var type = Substitute.For<Type>();
		type.Name.Returns(aggregateName);
		type.FullName.ReturnsNull();

		// Act
		var result = TypeNameHelper.GetName(type, trimNamePart);

		// Assert
		result.ShouldBe(expectation);
	}
}
