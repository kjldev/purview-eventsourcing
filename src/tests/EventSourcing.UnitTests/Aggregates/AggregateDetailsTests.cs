using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing;

public class AggregateDetailsTests
{
	[Test]
	public async Task Id_WhenSetFirstTime_StoresValue(CancellationToken cancellationToken)
	{
		// Arrange
		var details = new AggregateDetails();

		// Act
		details.Id = "test-id";

		// Assert
		await Assert.That(details.Id).IsEqualTo("test-id");
	}

	[Test]
	public async Task Id_WhenSetToSameValue_DoesNotThrow(CancellationToken cancellationToken)
	{
		// Arrange
		var details = new AggregateDetails { Id = "test-id" };

		// Act — setting same value should not throw
		details.Id = "test-id";

		// Assert
		await Assert.That(details.Id).IsEqualTo("test-id");
	}

	[Test]
	public async Task DefaultValues_AreCorrect(CancellationToken cancellationToken)
	{
		// Act
		var details = new AggregateDetails();

		// Assert
		await Assert.That(details.Id).IsNull();
		await Assert.That(details.SavedVersion).IsEqualTo(0);
		await Assert.That(details.CurrentVersion).IsEqualTo(0);
		await Assert.That(details.IsDeleted).IsFalse();
		await Assert.That(details.Locked).IsFalse();
	}

	[Test]
	public async Task Clone_CreatesIndependentCopy(CancellationToken cancellationToken)
	{
		// Arrange
		var original = new AggregateDetails
		{
			Id = "clone-test",
			SavedVersion = 5,
			CurrentVersion = 7,
			IsDeleted = true,
			Etag = "etag-value"
		};

		// Act
		var clone = (AggregateDetails)original.Clone();

		// Assert
		await Assert.That(clone.Id).IsEqualTo(original.Id);
		await Assert.That(clone.SavedVersion).IsEqualTo(original.SavedVersion);
		await Assert.That(clone.CurrentVersion).IsEqualTo(original.CurrentVersion);
		await Assert.That(clone.IsDeleted).IsEqualTo(original.IsDeleted);
		await Assert.That(clone.Etag).IsEqualTo(original.Etag);
	}

	[Test]
	public async Task Clone_ModifyingClone_DoesNotAffectOriginal(CancellationToken cancellationToken)
	{
		// Arrange
		var original = new AggregateDetails
		{
			Id = "original",
			SavedVersion = 3,
			CurrentVersion = 5
		};

		// Act
		var clone = (AggregateDetails)original.Clone();
		clone.SavedVersion = 99;
		clone.CurrentVersion = 100;

		// Assert — original unchanged
		await Assert.That(original.SavedVersion).IsEqualTo(3);
		await Assert.That(original.CurrentVersion).IsEqualTo(5);
	}

	[Test]
	public async Task GetHashCode_GivenSameProperties_ReturnsConsistentHash(CancellationToken cancellationToken)
	{
		// Arrange
		var details = new AggregateDetails
		{
			Id = "hash-test",
			SavedVersion = 1,
			CurrentVersion = 3
		};

		// Act
		var hash1 = details.GetHashCode();
		var hash2 = details.GetHashCode();

		// Assert
		await Assert.That(hash1).IsEqualTo(hash2);
	}
}
