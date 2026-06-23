using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain;

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

sealed class AggregateAuditService(IEventStore eventStore) : IAggregateAuditService
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

	public async Task<IReadOnlyList<AggregateEventHistoryItem>> GetLatestHistoryAsync(
		string aggregateType,
		AggregateEventHistoryRequest request,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(request);

		var maxRecords = request.MaxRecords is < 1 or > 1000
			? ContinuationRequest.DefaultMaxRecords
			: request.MaxRecords;
		var candidates = NormalizeAggregateType(aggregateType) switch
		{
			"CUSTOMER" => await GetEventsAcrossAggregatesAsync<CustomerAggregate>(
				request,
				maxRecords,
				cancellationToken
			),
			"ORDER" => await GetEventsAcrossAggregatesAsync<OrderAggregate>(request, maxRecords, cancellationToken),
			"INVENTORY" => await GetEventsAcrossAggregatesAsync<InventoryAggregate>(
				request,
				maxRecords,
				cancellationToken
			),
			"LOCATION" => await GetEventsAcrossAggregatesAsync<LocationAggregate>(
				request,
				maxRecords,
				cancellationToken
			),
			"REPORTUPLOAD" => await GetEventsAcrossAggregatesAsync<ReportUploadAggregate>(
				request,
				maxRecords,
				cancellationToken
			),
			_ => throw new ArgumentOutOfRangeException(
				nameof(aggregateType),
				aggregateType,
				$"Unsupported aggregate type '{aggregateType}'."
			),
		};

		return
		[
			.. candidates.OrderByDescending(m => m.When).ThenByDescending(m => m.AggregateVersion).Take(maxRecords),
		];
	}

	static string NormalizeAggregateType(string aggregateType) =>
		aggregateType?.Trim().ToUpperInvariant() ?? string.Empty;

	async Task<IReadOnlyList<AggregateEventHistoryItem>> GetEventsAcrossAggregatesAsync<T>(
		AggregateEventHistoryRequest request,
		int maxRecords,
		CancellationToken cancellationToken
	)
		where T : class, IAggregate, new()
	{
		List<AggregateEventHistoryItem> items = [];
		await foreach (var aggregateId in eventStore.GetAggregateIdsAsync<T>(includeDeleted: false, cancellationToken))
		{
			if (string.IsNullOrWhiteSpace(aggregateId))
				continue;

			var history = await eventStore.GetEventHistoryAsync<T>(
				aggregateId,
				new AggregateEventHistoryRequest
				{
					FromVersion = request.FromVersion,
					ToVersion = request.ToVersion,
					FromUtc = request.FromUtc,
					ToUtc = request.ToUtc,
					MaxRecords = maxRecords,
				},
				cancellationToken
			);

			if (history.ResultCount == 0)
				continue;

			items.AddRange(history.Results);
		}

		return items;
	}
}
