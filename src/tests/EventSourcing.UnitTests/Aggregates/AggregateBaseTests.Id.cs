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
		var action = () => aggregate.Details.Id = "Another Id";

		// Assert
		await Assert.That(action).Throws<IdAlreadySetException>();
	}

	[Test]
	public async Task Id_GivenIdAlreadySetAndIdIsSetToTheSame_DoesNotThrowException()
	{
		// Arrange
		const string aggregateId = "Aggregate-Id";

		var aggregate = CreateTestAggregate(aggregateId);

		// Act
		var action = () => aggregate.Details.Id = aggregateId;

		// Assert
		await Assert.That(action).ThrowsNothing();
	}
}
