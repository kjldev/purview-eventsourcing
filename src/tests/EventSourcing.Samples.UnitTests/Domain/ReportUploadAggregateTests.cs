namespace Purview.EventSourcing.Samples.Domain;

public sealed class ReportUploadAggregateTests
{
	[Test]
	public async Task MarkAsComplete_GivenReportIsMarkedAsComplete_SetsStatusToCompleteAsSideEffect()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateValidBlobUri());
		await Assert.That(sut.Status).IsNotEqualTo(ReportProcessingStatus.Complete);

		// Act (status is not passed by caller)
		sut.MarkAsCompleted(CreateValidBlobUri(), new object());

		// Assert
		await Assert.That(sut.Status).IsEqualTo(ReportProcessingStatus.Complete);
	}

	[Test]
	public async Task MarkAsComplete_GivenReportIsMarkedAsComplete_RecordsStatusInEvent()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateValidBlobUri());

		// Act
		sut.MarkAsCompleted(CreateValidBlobUri(), new object());

		// Assert (event contains the computed status value)
		var completedEvent = sut.GetUnsavedEvents()
			.Single(@event => @event.GetType().GetProperty("Status") is not null);
		var statusProperty = completedEvent.GetType().GetProperty("Status");
		await Assert.That(statusProperty).IsNotNull();
		await Assert.That(statusProperty!.GetValue(completedEvent)).IsEqualTo(ReportProcessingStatus.Complete);
	}

	[Test]
	public void MarkAsComplete_GivenCallerSetsComputedStatus_ThrowsArgumentException()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateValidBlobUri());

		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			sut.MarkAsCompleted(CreateValidBlobUri(), new object(), ReportProcessingStatus.Failed)
		);
	}

	static ReportUploadAggregate CreateSUT() => new();

	static string CreateValidBlobUri() => $"/example/nesting/{Guid.NewGuid()}/blob.json";
}
