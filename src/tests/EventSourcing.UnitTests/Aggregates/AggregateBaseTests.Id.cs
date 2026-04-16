using Purview.EventSourcing.Aggregates.Exceptions;

namespace Purview.EventSourcing.Aggregates;

partial class AggregateBaseTests
{
	[Test]
	public async Task Id_GivenIdAlreadySet_ThrowsArgumentIdAlreadySetException()
	{
		// Arrange
		var aggregate = CreateTestAggregate("Aggregate-Id");

		// Act
		string Action() => aggregate.Details.Id = "Another Id";

		// Assert
		await Assert.That(Action).Throws<IdAlreadySetException>();
	}

	[Test]
	public async Task Id_GivenIdAlreadySetAndIdIsSetToTheSame_DoesNotThrowException()
	{
		// Arrange
		const string aggregateId = "Aggregate-Id";

		var aggregate = CreateTestAggregate(aggregateId);

		// Act
		string Action() => aggregate.Details.Id = aggregateId;

		// Assert
		await Assert.That(Action).ThrowsNothing();
	}
}
