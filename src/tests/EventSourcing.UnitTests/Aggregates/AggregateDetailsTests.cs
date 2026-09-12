using Purview.EventSourcing.Aggregates.Exceptions;

namespace Purview.EventSourcing.Aggregates;

public class AggregateDetailsTests
{
	[Test]
	public async Task Id_WhenChangedAfterSet_ThrowsIdAlreadySetException()
	{
		// Arrange
		AggregateDetails details = new() { Id = "test-id" };

		// Act
		string ChangeId() => details.Id = "another-id";

		// Assert
		await Assert.That(ChangeId).Throws<IdAlreadySetException>();
	}

	[Test]
	public async Task Locked_WhenUnlockedAfterBeingLocked_ThrowsLockedException()
	{
		// Arrange
		AggregateDetails details = new() { Id = "test-id", Locked = true };

		// Act
		bool Unlock() => details.Locked = false;

		// Assert
		await Assert.That(Unlock).Throws<LockedException>();
	}

	[Test]
	public async Task Clone_ModifyingClone_DoesNotAffectOriginal()
	{
		// Arrange
		AggregateDetails original = new()
		{
			Id = "original",
			SavedVersion = 3,
			CurrentVersion = 5,
			Created = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
			Updated = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
		};

		// Act
		var clone = (AggregateDetails)original.Clone();
		clone.SavedVersion = 99;
		clone.CurrentVersion = 100;
		clone.Updated = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero);

		// Assert — original unchanged
		await Assert.That(original.SavedVersion).IsEqualTo(3);
		await Assert.That(original.CurrentVersion).IsEqualTo(5);
		await Assert.That(original.Updated).IsEqualTo(new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero));
	}

	[Test]
	public async Task Clone_PreservesUpdatedTimestamp()
	{
		// Arrange
		DateTimeOffset expectedUpdated = new(2026, 2, 10, 9, 30, 0, TimeSpan.Zero);
		AggregateDetails original = new() { Id = "original", Updated = expectedUpdated };

		// Act
		var clone = (AggregateDetails)original.Clone();

		// Assert
		await Assert.That(clone.Updated).IsEqualTo(expectedUpdated);
	}
}
