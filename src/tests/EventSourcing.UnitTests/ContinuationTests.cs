namespace Purview.EventSourcing;

public class ContinuationTests
{
	[Test]
	public async Task ContinuationResponse_HasRecords_GivenEmptyResults_ReturnsFalse()
	{
		// Arrange
		var response = new ContinuationResponse<string> { Results = [], RequestedCount = 10 };

		// Assert
		await Assert.That(response.HasRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_HasRecords_GivenResults_ReturnsTrue()
	{
		// Arrange
		var response = new ContinuationResponse<string> { Results = ["item1", "item2"], RequestedCount = 10 };

		// Assert
		await Assert.That(response.HasRecords).IsTrue();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenContinuationToken_ReturnsTrue()
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item1"],
			RequestedCount = 1,
			ContinuationToken = "next-token",
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsTrue();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenNoContinuationToken_ReturnsFalse()
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item1"],
			RequestedCount = 10,
			ContinuationToken = null,
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_ToRequest_CreatesRequestWithTokenAndCount()
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item"],
			RequestedCount = 25,
			ContinuationToken = "page-2",
		};

		// Act
		var request = response.ToRequest();

		// Assert
		await Assert.That(request.ContinuationToken).IsEqualTo("page-2");
		await Assert.That(request.MaxRecords).IsEqualTo(25);
	}

	[Test]
	public async Task ContinuationResponse_Convert_MapsResultsCorrectly()
	{
		// Arrange
		var response = new ContinuationResponse<int>
		{
			Results = [1, 2, 3],
			RequestedCount = 10,
			ContinuationToken = "token",
		};

		// Act
		var converted = response.Convert(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));

		// Assert
		await Assert.That(converted.Results).Count().IsEqualTo(3);
		await Assert.That(converted.ContinuationToken).IsEqualTo("token");
		await Assert.That(converted.RequestedCount).IsEqualTo(10);
	}
}
