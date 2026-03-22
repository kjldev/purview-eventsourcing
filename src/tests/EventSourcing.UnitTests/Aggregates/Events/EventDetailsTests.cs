using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing;

public class EventDetailsTests
{
	[Test]
	public async Task DefaultValues_AreCorrect(CancellationToken cancellationToken)
	{
		// Act
		var details = new EventDetails();

		// Assert
		await Assert.That(details.IdempotencyId).IsNull();
		await Assert.That(details.AggregateVersion).IsEqualTo(0);
		await Assert.That(details.UserId).IsNull();
	}

	[Test]
	public async Task Properties_CanBeSet(CancellationToken cancellationToken)
	{
		// Arrange
		var when = DateTimeOffset.UtcNow;

		// Act
		var details = new EventDetails
		{
			IdempotencyId = "idempotency-123",
			AggregateVersion = 5,
			When = when,
			UserId = "user-1"
		};

		// Assert
		await Assert.That(details.IdempotencyId).IsEqualTo("idempotency-123");
		await Assert.That(details.AggregateVersion).IsEqualTo(5);
		await Assert.That(details.When).IsEqualTo(when);
		await Assert.That(details.UserId).IsEqualTo("user-1");
	}

	[Test]
	public async Task GetHashCode_GivenSameValues_IsConsistent(CancellationToken cancellationToken)
	{
		// Arrange
		var details = new EventDetails
		{
			AggregateVersion = 3,
			When = DateTimeOffset.UtcNow,
			UserId = "test"
		};

		// Act
		var hash1 = details.GetHashCode();
		var hash2 = details.GetHashCode();

		// Assert
		await Assert.That(hash1).IsEqualTo(hash2);
	}
}
