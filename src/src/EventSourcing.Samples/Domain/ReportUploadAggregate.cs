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

	public UserCaptureRecord Uploaded { get; private set; } = UserCaptureRecord.Empty;

	public ReportProcessingStatus Status { get; private set; } = ReportProcessingStatus.None;

	public string? FailureReason { get; private set; }

	public object? ReportSummary { get; private set; }

	public EventStoreSet<ProjectId> RelatedProjects { get; private set; } = [];

	public EventStoreList<StatusHistory> StatusHistory { get; private set; } = [];

	public ProjectId? CustomerProjectId { get; private set; }

	// Wrapped mutation methods
	public ReportUploadAggregate MarkAsFailed(string? failureReason = null) =>
		SetReportProcessingStatus(ReportProcessingStatus.Failed, failureReason);

	public ReportUploadAggregate MarkAsProcessing() => SetReportProcessingStatus(ReportProcessingStatus.Processing);

	public ReportUploadAggregate ChangeCustomerProjectId(ProjectId? customerProjectId = null) =>
		customerProjectId is null ? ClearCustomerProjectId() : SetCustomerProjectId(customerProjectId.Value);

	// Mutation methods
	[GenerateAggregateEvent(EventName = "MarkAsCompleted")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Purview.EventSourcing.SourceGenerator",
		"EVENTSTORE016:Event parameter nullability differs from aggregate property",
		Justification = "Required properties"
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
		UserCaptureRecord uploaded
	);

	[GenerateAggregateEvent]
	private partial ReportUploadAggregate SetReportProcessingStatus(
		ReportProcessingStatus status,
		string? failureReason = null
	);

	[GenerateAggregateCollectionEvent(nameof(RelatedProjects))]
	public partial ReportUploadAggregate AddRelatedProject(ProjectId projectId);

	[GenerateAggregateCollectionEvent(nameof(RelatedProjects))]
	public partial ReportUploadAggregate AddRelatedProjects(IEnumerable<ProjectId> projectIds);

	[GenerateAggregateCollectionEvent(nameof(RelatedProjects))]
	public partial ReportUploadAggregate AddRelatedProjects(params ProjectId[] projectIds);

	[GenerateAggregateCollectionEvent(nameof(StatusHistory))]
	public partial ReportUploadAggregate AddStatusHistory(StatusHistory statusHistory);

	[GenerateAggregateCollectionEvent(nameof(StatusHistory))]
	public partial ReportUploadAggregate AddStatusHistories(IEnumerable<StatusHistory> statusHistories);

	[GenerateAggregateCollectionEvent(nameof(StatusHistory))]
	public partial ReportUploadAggregate AddStatusHistories(params StatusHistory[] statusHistories);

	[GenerateAggregateEvent(Manual = true)]
	public partial ReportUploadAggregate SetReportSummary(EnvironmentVariableTarget target);

	[GenerateAggregateEvent]
	private partial ReportUploadAggregate SetCustomerProjectId(ProjectId customerProjectId);

	[GenerateAggregateEvent]
	private partial ReportUploadAggregate ClearCustomerProjectId();

	// Appliers
	private partial void Apply(ReportUploadEvents.ReportSummarySetEvent @event) => ReportSummary = @event.Target;

	private partial void Apply(ReportUploadEvents.CustomerProjectIdClearedEvent @event) => CustomerProjectId = null;
}
