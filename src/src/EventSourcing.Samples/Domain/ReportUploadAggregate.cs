using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.Domain;

[GenerateAggregate]
public sealed partial class ReportUploadAggregate : AggregateBase
{
	public string SourceJsonBlob { get; private set; } = string.Empty;

	public string? ExcelReportBlob { get; private set; }

	public ReportProcessingStatus Status { get; private set; } = ReportProcessingStatus.Uploaded;

	public object? ReportSummary { get; private set; }

	[GenerateAggregateEvent]
	public partial ReportUploadAggregate Create(string sourceJsonBlob);

	[GenerateAggregateEvent(EventName = "CompletedEvent")]
	public partial ReportUploadAggregate MarkAsCompleted(
		string excelReportBlob,
		object reportSummary,
		[Computed] ReportProcessingStatus status = default
	);

	partial void OnComputingCompletedEvent(ref ReportProcessingStatus status) =>
		status = ReportProcessingStatus.Complete;
}

public enum ReportProcessingStatus
{
	Uploaded = 0,
	Processing = 1,
	Complete = 2,
	Failed = 3,
}
