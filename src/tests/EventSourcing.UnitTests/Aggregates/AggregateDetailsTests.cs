namespace Purview.EventSourcing.Aggregates;

public class AggregateDetailsTests
{
	[Test]
	public async Task Id_WhenSetToSameValue_DoesNotThrow()
	{
		// Arrange
		var details = new AggregateDetails { Id = "test-id" };

		// Act — setting same value should not throw
		details.Id = "test-id";

		// Assert
		await Assert.That(details.Id).IsEqualTo("test-id");
	}

	[Test]
	public async Task Clone_ModifyingClone_DoesNotAffectOriginal()
	{
		// Arrange
		var original = new AggregateDetails
		{
			Id = "original",
			SavedVersion = 3,
			CurrentVersion = 5,
		};

		// Act
		var clone = (AggregateDetails)original.Clone();
		clone.SavedVersion = 99;
		clone.CurrentVersion = 100;

		// Assert — original unchanged
		await Assert.That(original.SavedVersion).IsEqualTo(3);
		await Assert.That(original.CurrentVersion).IsEqualTo(5);
	}
}
