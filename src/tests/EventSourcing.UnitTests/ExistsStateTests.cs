namespace Purview.EventSourcing;

public class ExistsStateTests
{
	[Test]
	public async Task ImplicitBoolConversion_GivenExists_ReturnsTrue()
	{
		// Act
		bool result = ExistsState.Exists;

		// Assert
		await Assert.That(result).IsTrue();
	}

	[Test]
	public async Task ImplicitBoolConversion_GivenDoesNotExist_ReturnsFalse()
	{
		// Act
		bool result = ExistsState.DoesNotExist;

		// Assert
		await Assert.That(result).IsFalse();
	}

	[Test]
	public async Task ImplicitBoolConversion_GivenExistsInDeletedState_ReturnsTrue()
	{
		// Act
		bool result = ExistsState.ExistsInDeletedState;

		// Assert
		await Assert.That(result).IsTrue();
	}
}
