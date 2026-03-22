namespace Purview.EventSourcing;

public class ContinuationTests
{
	#region ContinuationRequest Tests

	[Test]
	public async Task ContinuationRequest_DefaultMaxRecords_Is20(CancellationToken cancellationToken)
	{
		// Act
		var request = new ContinuationRequest();

		// Assert
		await Assert.That(request.MaxRecords).IsEqualTo(20);
	}

	[Test]
	public async Task ContinuationRequest_DefaultToken_IsNull(CancellationToken cancellationToken)
	{
		// Act
		var request = new ContinuationRequest();

		// Assert
		await Assert.That(request.ContinuationToken).IsNull();
	}

	[Test]
	public async Task ContinuationRequest_CanSetMaxRecords(CancellationToken cancellationToken)
	{
		// Act
		var request = new ContinuationRequest { MaxRecords = 50 };

		// Assert
		await Assert.That(request.MaxRecords).IsEqualTo(50);
	}

	[Test]
	public async Task ContinuationRequest_CanSetContinuationToken(CancellationToken cancellationToken)
	{
		// Act
		var request = new ContinuationRequest { ContinuationToken = "next-page-token" };

		// Assert
		await Assert.That(request.ContinuationToken).IsEqualTo("next-page-token");
	}

	#endregion

	#region ContinuationResponse Tests

	[Test]
	public async Task ContinuationResponse_HasRecords_GivenEmptyResults_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = [],
			RequestedCount = 10
		};

		// Assert
		await Assert.That(response.HasRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_HasRecords_GivenResults_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item1", "item2"],
			RequestedCount = 10
		};

		// Assert
		await Assert.That(response.HasRecords).IsTrue();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenContinuationToken_ReturnsTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item1"],
			RequestedCount = 1,
			ContinuationToken = "next-token"
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsTrue();
	}

	[Test]
	public async Task ContinuationResponse_HasMoreRecords_GivenNoContinuationToken_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item1"],
			RequestedCount = 10,
			ContinuationToken = null
		};

		// Assert
		await Assert.That(response.HasMoreRecords).IsFalse();
	}

	[Test]
	public async Task ContinuationResponse_ResultCount_ReturnsCorrectCount(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["a", "b", "c"],
			RequestedCount = 10
		};

		// Assert
		await Assert.That(response.ResultCount).IsEqualTo(3);
	}

	[Test]
	public async Task ContinuationResponse_ToRequest_CreatesRequestWithTokenAndCount(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<string>
		{
			Results = ["item"],
			RequestedCount = 25,
			ContinuationToken = "page-2"
		};

		// Act
		var request = response.ToRequest();

		// Assert
		await Assert.That(request.ContinuationToken).IsEqualTo("page-2");
		await Assert.That(request.MaxRecords).IsEqualTo(25);
	}

	[Test]
	public async Task ContinuationResponse_Convert_MapsResultsCorrectly(CancellationToken cancellationToken)
	{
		// Arrange
		var response = new ContinuationResponse<int>
		{
			Results = [1, 2, 3],
			RequestedCount = 10,
			ContinuationToken = "token"
		};

		// Act
		var converted = response.Convert(i => i.ToString());

		// Assert
		await Assert.That(converted.Results).HasCount().EqualTo(3);
		await Assert.That(converted.ContinuationToken).IsEqualTo("token");
		await Assert.That(converted.RequestedCount).IsEqualTo(10);
	}

	#endregion
}
