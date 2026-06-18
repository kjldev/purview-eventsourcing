using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

[GenerateAggregate]
public sealed partial class ReportUploadAggregate : AggregateBase
{
	public ProjectId ProjectId { get; private set; } = ProjectId.Empty;

	public string OriginalFilename { get; private set; } = string.Empty;

	public BlobUri SourceJsonBlob { get; private set; } = BlobUri.Empty;

	public BlobUri? ExcelReportBlob { get; private set; }

	public UserCapture Uploaded { get; private set; } = UserCapture.Empty;

	public ReportProcessingStatus Status { get; private set; } = ReportProcessingStatus.None;

	public string? FailureReason { get; private set; }

	public object? ReportSummary { get; private set; }

	public ReportUploadAggregate MarkAsFailed(string? failureReason = null) =>
		SetReportProcessingStatus(ReportProcessingStatus.Failed, failureReason);

	public ReportUploadAggregate MarkAsProcessing() => SetReportProcessingStatus(ReportProcessingStatus.Processing);

	// Event generation methods
	[GenerateAggregateEvent(EventName = "MarkAsCompleted")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Purview.EventSourcing.SourceGenerator",
		"EVENTSTORE016:Event parameter nullability differs from aggregate property",
		Justification = "Reqiured properties"
	)]
	public partial ReportUploadAggregate MarkAsComplete(
		BlobUri excelReportBlob,
		object reportSummary,
		[Computed] ReportProcessingStatus status = default
	);

	[GenerateAggregateEvent]
	public partial ReportUploadAggregate Create(
		ProjectId projectId,
		string originalFilename,
		BlobUri sourceJsonBlob,
		UserCapture uploaded
	);

	[GenerateAggregateEvent]
	private partial ReportUploadAggregate SetReportProcessingStatus(
		ReportProcessingStatus status,
		string? failureReason = null
	);
}
