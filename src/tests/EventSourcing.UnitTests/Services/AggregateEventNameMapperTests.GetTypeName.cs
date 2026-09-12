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
	public async Task GetTypeName_GivenEventTypeNameIsNullOrWhitespace_ThrowsArgumentException(string? eventTypeName)
	{
		// Arrange
		var mapper = CreateMapper<CorrectlyNamedAggregate>();

		// Act
		string? Action() => mapper.GetTypeName<CorrectlyNamedAggregate>(eventTypeName!);

		// Assert
		await Assert.That(Action).Throws<ArgumentException>().WithParameterName(nameof(eventTypeName));
	}
}
