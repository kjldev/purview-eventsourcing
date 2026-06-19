using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

partial class ReportUploadAggregate
{
	partial void OnRaisingCreatedEvent(
		ref ProjectId projectId,
		ref string originalFilename,
		ref BlobUri sourceJsonBlob,
		ref UserCapture uploaded
	)
	{
		if (Details.CurrentVersion > 0)
			throw new InvalidOperationException("Cannot create a report upload for an existing project.");

		ArgumentException.ThrowIfNullOrWhiteSpace(originalFilename);
		ArgumentNullException.ThrowIfNull(sourceJsonBlob);
	}

	partial void OnUploadedChanging(ref UserCapture uploaded)
	{
		if (uploaded == UserCapture.Empty)
			throw new InvalidOperationException("Uploaded information cannot be empty.");

		if (Details.CurrentVersion > 0 && Uploaded.IsEssentialChange(uploaded))
			throw new InvalidOperationException("Cannot change uploaded information after report creation.");
	}

	partial void OnComputingMarkAsCompletedEvent(ref ReportProcessingStatus status) =>
		status = ReportProcessingStatus.Completed;

	partial void OnRaisingMarkAsCompletedEvent(ref BlobUri? excelReportBlob, ref object? reportSummary)
	{
		ArgumentNullException.ThrowIfNull(excelReportBlob);
		ArgumentNullException.ThrowIfNull(reportSummary);
	}

	partial void OnRaisingReportProcessingStatusSetEvent(ref ReportProcessingStatus status, ref string? failureReason)
	{
		if (status == ReportProcessingStatus.Completed)
			throw new InvalidOperationException(
				$"Use the {nameof(MarkAsComplete)} method to set the report as complete."
			);

		if (status != ReportProcessingStatus.Failed)
		{
			if (!string.IsNullOrWhiteSpace(failureReason))
				throw new InvalidOperationException("Failure reason must be provided when marking as failed.");

			// In-case of empty strings, we'll just null it out here. Cleaner.
			failureReason = null;
		}
	}
}
