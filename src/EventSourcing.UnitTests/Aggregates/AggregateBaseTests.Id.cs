using Purview.EventSourcing.Aggregates.Exceptions;

namespace Purview.EventSourcing.Aggregates;

partial class AggregateBaseTests
{
	[Fact]
	public void Id_GivenIdAlreadySet_ThrowsArgumentIdAlreadySetException()
	{
		// Arrange
		var aggregate = CreateTestAggregate("Aggregate-Id");

		// Act
		var action = () => aggregate.Details.Id = "Another Id";

		// Assert
		action.ShouldThrow<IdAlreadySetException>();
	}

	[Fact]
	public void Id_GivenIdAlreadySetAndIdIsSetToTheSame_DoesNotThrowException()
	{
		// Arrange
		const string aggregateId = "Aggregate-Id";

		var aggregate = CreateTestAggregate(aggregateId);

		// Act
		var action = () => aggregate.Details.Id = aggregateId;

		// Assert
		action.ShouldNotThrow();
	}
}
