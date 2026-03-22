namespace Purview.EventSourcing;

public partial class AggregateBaseTests
{
	[Test]
	public async Task ForceSave_GivenNoUnsavedEvents_RecordsForceSaveEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();

		// Act
		aggregate.ForceSave();

		// Assert
		await Assert.That(aggregate.HasUnsavedEvents()).IsTrue();
		await Assert.That(aggregate.GetUnsavedEvents().Count()).IsEqualTo(1);
	}

	[Test]
	public async Task ForceSave_GivenExistingUnsavedEvents_DoesNotRecordForceSaveEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();
		aggregate.RecordEvent();

		var eventCountBefore = aggregate.GetUnsavedEvents().Count();

		// Act
		aggregate.ForceSave();

		// Assert — no additional event added since unsaved events already exist
		await Assert.That(aggregate.GetUnsavedEvents().Count()).IsEqualTo(eventCountBefore);
	}

	[Test]
	public async Task HasUnsavedEvents_GivenNoEvents_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();

		// Assert
		await Assert.That(aggregate.HasUnsavedEvents()).IsFalse();
	}

	[Test]
	public async Task HasUnsavedEvents_GivenRecordedEvent_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();
		aggregate.RecordEvent();

		// Assert
		await Assert.That(aggregate.HasUnsavedEvents()).IsTrue();
	}

	[Test]
	public async Task IsNew_GivenNewAggregate_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();

		// Assert
		await Assert.That(aggregate.IsNew()).IsTrue();
	}

	[Test]
	public async Task AggregateType_UsesTypeNameHelper_RemovesAggregateSuffix(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = new Aggregates.Test.TestAggregate();

		// Assert — "TestAggregate" becomes "test" via TypeNameHelper
		await Assert.That(aggregate.AggregateType).IsEqualTo("test");
	}
}
