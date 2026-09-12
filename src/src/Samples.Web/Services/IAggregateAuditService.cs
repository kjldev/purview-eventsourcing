namespace Purview.EventSourcing.Samples.Web.Services;

interface IAggregateAuditService
{
	Task<ContinuationResponse<AggregateEventHistoryItem>> GetHistoryAsync(
		string aggregateType,
		string aggregateId,
		AggregateEventHistoryRequest request,
		CancellationToken cancellationToken
	);

	Task<IReadOnlyList<AggregateEventHistoryItem>> GetLatestHistoryAsync(
		string aggregateType,
		AggregateEventHistoryRequest request,
		CancellationToken cancellationToken
	);
}
