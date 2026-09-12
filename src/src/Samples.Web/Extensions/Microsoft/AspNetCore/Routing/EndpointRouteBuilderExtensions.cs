using Purview.EventSourcing.Samples.Web.Services;

namespace Microsoft.AspNetCore.Routing;

static class EndpointRouteBuilderExtensions
{
	extension(IEndpointRouteBuilder builder)
	{
		public IEndpointRouteBuilder MapAudit()
		{
			builder
				.MapGroup("/api/audit")
				.MapGet(
					"/aggregates/{aggregateType}/{aggregateId}/events",
					static async Task<IResult> (
						string aggregateType,
						string aggregateId,
						int? fromVersion,
						int? toVersion,
						DateTimeOffset? fromUtc,
						DateTimeOffset? toUtc,
						int? maxRecords,
						string? continuationToken,
						IAggregateAuditService auditService,
						CancellationToken cancellationToken
					) =>
					{
						if (!AggregateAuditService.IsSupportedAggregateType(aggregateType))
							return Results.BadRequest(
								new
								{
									Error = $"Unsupported aggregate type '{aggregateType}'.",
									AggregateAuditService.SupportedAggregateTypes,
								}
							);
						AggregateEventHistoryRequest request = new()
						{
							FromVersion = fromVersion,
							ToVersion = toVersion,
							FromUtc = fromUtc,
							ToUtc = toUtc,
							MaxRecords = maxRecords ?? ContinuationRequest.DefaultMaxRecords,
							ContinuationToken = continuationToken,
						};
						var response = await auditService.GetHistoryAsync(
							aggregateType,
							aggregateId,
							request,
							cancellationToken
						);
						return Results.Ok(response);
					}
				);
			return builder;
		}
	}
}
