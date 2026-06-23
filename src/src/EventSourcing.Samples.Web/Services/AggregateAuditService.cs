using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Services;

sealed class AggregateAuditService(IEventStore eventStore)
{
	public static IReadOnlyList<string> SupportedAggregateTypes { get; } =
	["customer", "order", "inventory", "location", "reportupload"];

	public static bool IsSupportedAggregateType(string? aggregateType) =>
		!string.IsNullOrWhiteSpace(aggregateType)
		&& SupportedAggregateTypes.Any(m => m.Equals(aggregateType.Trim(), StringComparison.OrdinalIgnoreCase));

	public Task<ContinuationResponse<AggregateEventHistoryItem>> GetHistoryAsync(
		string aggregateType,
		string aggregateId,
		AggregateEventHistoryRequest request,
		CancellationToken cancellationToken
	) =>
		NormalizeAggregateType(aggregateType) switch
		{
			"CUSTOMER" => eventStore.GetEventHistoryAsync<CustomerAggregate>(aggregateId, request, cancellationToken),
			"ORDER" => eventStore.GetEventHistoryAsync<OrderAggregate>(aggregateId, request, cancellationToken),
			"INVENTORY" => eventStore.GetEventHistoryAsync<InventoryAggregate>(aggregateId, request, cancellationToken),
			"LOCATION" => eventStore.GetEventHistoryAsync<LocationAggregate>(aggregateId, request, cancellationToken),
			"REPORTUPLOAD" => eventStore.GetEventHistoryAsync<ReportUploadAggregate>(
				aggregateId,
				request,
				cancellationToken
			),
			_ => throw new ArgumentOutOfRangeException(
				nameof(aggregateType),
				aggregateType,
				$"Unsupported aggregate type '{aggregateType}'."
			),
		};

	static string NormalizeAggregateType(string aggregateType) =>
		aggregateType?.Trim().ToUpperInvariant() ?? string.Empty;
}
