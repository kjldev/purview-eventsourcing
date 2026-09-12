namespace Purview.EventSourcing;

public class ContinuationTests
{
	[Test]
	public async Task ContinuationResponse_HasRecords_GivenEmptyResults_ReturnsFalse()
	{
		// Arrange
		ContinuationResponse<string> response = new() { Results = [], RequestedCount = 10 };

		// Assert
		await Assert.That(response.HasRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_HasRecords_GivenResults_ReturnsTrue()
	{
		// Arrange
		ContinuationResponse<string> response = new() { Results = ["item1", "item2"], RequestedCount = 10 };

		// Assert
		await Assert.That(response.HasRecords).IsTrue();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenContinuationToken_ReturnsTrue()
	{
		// Arrange
		ContinuationResponse<string> response = new()
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
		ContinuationResponse<string> response = new()
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
		ContinuationResponse<string> response = new()
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
	public async Task ContinuationResponse_HasMoreRecords_GivenEmptyStringToken_ReturnsFalse()
	{
		// Arrange
		ContinuationResponse<string> response = new()
		{
			Results = ["item1"],
			RequestedCount = 10,
			ContinuationToken = "",
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenWhitespaceToken_ReturnsFalse()
	{
		// Arrange
		ContinuationResponse<string> response = new()
		{
			Results = ["item1"],
			RequestedCount = 10,
			ContinuationToken = "   ",
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_Convert_MapsResultsCorrectly()
	{
		// Arrange
		ContinuationResponse<int> response = new()
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
		await Assert.That(converted.Results[0]).IsEqualTo("1");
		await Assert.That(converted.Results[1]).IsEqualTo("2");
		await Assert.That(converted.Results[2]).IsEqualTo("3");
	}

	[Test]
	public async Task ContinuationResponse_Convert_PreservesTotalCount()
	{
		// Arrange
		ContinuationResponse<int> response = new()
		{
			Results = [1, 2],
			RequestedCount = 10,
			TotalCount = 150,
			ContinuationToken = "token",
		};

		// Act
		var converted = response.Convert(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));

		// Assert — TotalCount should be preserved through conversion
		await Assert.That(converted.TotalCount).IsEqualTo(150);
	}

	[Test]
	public async Task ContinuationResponse_Convert_TransformsType()
	{
		// Arrange
		ContinuationResponse<int> response = new() { Results = [100, 200, 300], RequestedCount = 10 };

		// Act
		var converted = response.Convert(i => $"Value-{i}");

		// Assert — type transformation works
		await Assert.That(converted.Results).Count().IsEqualTo(3);
		await Assert.That(converted.Results[0]).IsEqualTo("Value-100");
		await Assert.That(converted.Results[1]).IsEqualTo("Value-200");
		await Assert.That(converted.Results[2]).IsEqualTo("Value-300");
	}

	[Test]
	public async Task ContinuationResponse_Convert_GivenEmptyResults_ReturnsEmptyConverted()
	{
		// Arrange
		ContinuationResponse<int> response = new()
		{
			Results = [],
			RequestedCount = 10,
			ContinuationToken = "token",
		};

		// Act
		var converted = response.Convert(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));

		// Assert
		await Assert.That(converted.Results).IsEmpty();
		await Assert.That(converted.ContinuationToken).IsEqualTo("token");
	}

	[Test]
	public async Task ContinuationResponse_ImplicitCastToRequest_CreatesRequest()
	{
		// Arrange
		ContinuationResponse<string> response = new()
		{
			Results = ["item"],
			RequestedCount = 50,
			ContinuationToken = "next",
		};

		// Act
		ContinuationRequest request = response;

		// Assert
		await Assert.That(request.ContinuationToken).IsEqualTo("next");
		await Assert.That(request.MaxRecords).IsEqualTo(50);
	}
}
