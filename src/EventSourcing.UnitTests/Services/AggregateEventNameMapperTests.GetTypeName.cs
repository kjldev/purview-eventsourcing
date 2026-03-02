namespace Purview.EventSourcing;

partial class AggregateEventNameMapperTests
{
	[Test]
	public async Task GetTypeName_GivenEventTypeNameIsNotInCollection_ReturnsNull()
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();
		const string missingEventTypeName = "no-event-type";

		// Act
		var result = mapper.GetTypeName<CorrectlyNamedAggregate>(missingEventTypeName);

		// Assert
		await Assert.That(result).IsNull();
	}

	[Test]
	[Arguments("")]
	[Arguments(" ")]
	[Arguments("    ")]
	[Arguments(null)]
	public async Task GetTypeName_GivenEventTypeNameIsNullOrWhitespace_ThrowsArgumentNullException(
		string? eventTypeName
	)
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();

		// Act
		var action = () => mapper.GetTypeName<CorrectlyNamedAggregate>(eventTypeName!);

		// Assert
		var ex = await Assert.That(action).Throws<ArgumentNullException>();
		await Assert.That(ex.ParamName).IsEqualTo(nameof(eventTypeName));
	}
}
