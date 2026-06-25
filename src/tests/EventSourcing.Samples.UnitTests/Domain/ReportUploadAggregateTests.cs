using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

public sealed class ReportUploadAggregateTests
{
	[Test]
	public async Task MarkAsComplete_GivenReportIsMarkedAsComplete_SetsStatusToCompleteAsSideEffect()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateProjectId(), "report.json", CreateValidBlobUri(), CreateUploadedUser());
		await Assert.That(sut.Status).IsNotEqualTo(ReportProcessingStatus.Completed);

		// Act (status is not passed by caller)
		sut.MarkAsComplete(CreateValidBlobUri(), new object());

		// Assert
		await Assert.That(sut.Status).IsEqualTo(ReportProcessingStatus.Completed);
	}

	[Test]
	public async Task MarkAsComplete_GivenReportIsMarkedAsComplete_RecordsStatusInEvent()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateProjectId(), "report.json", CreateValidBlobUri(), CreateUploadedUser());

		// Act
		sut.MarkAsComplete(CreateValidBlobUri(), new object());

		// Assert (event contains the computed status value)
		var completedEvent = sut.GetUnsavedEvents()
			.Single(@event => @event.GetType().GetProperty("Status") is not null);
		var statusProperty = completedEvent.GetType().GetProperty("Status");
		await Assert.That(statusProperty).IsNotNull();
		await Assert.That(statusProperty!.GetValue(completedEvent)).IsEqualTo(ReportProcessingStatus.Completed);
	}

	[Test]
	public void MarkAsComplete_GivenCallerSetsComputedStatus_ThrowsArgumentException()
	{
		// Arrange
		var sut = CreateSUT();
		sut.Create(CreateProjectId(), "report.json", CreateValidBlobUri(), CreateUploadedUser());

		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			sut.MarkAsComplete(CreateValidBlobUri(), new object(), ReportProcessingStatus.Failed)
		);
	}

	static ReportUploadAggregate CreateSUT() => new();

	static ProjectId CreateProjectId() => ProjectId.Create(Guid.NewGuid().ToString());

	static BlobUri CreateValidBlobUri() =>
		BlobUri.Create(new Uri($"/example/nesting/{Guid.NewGuid()}/blob.json", UriKind.Relative));

	static UserCaptureRecord CreateUploadedUser() =>
		UserCaptureRecord.Create(UserDetails.Create(Guid.NewGuid(), "Uploader", true), DateTimeOffset.UtcNow);
}
