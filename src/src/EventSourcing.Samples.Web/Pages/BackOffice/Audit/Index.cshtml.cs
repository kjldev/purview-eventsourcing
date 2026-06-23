using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Audit;

sealed class IndexModel(IAggregateAuditService auditService) : PageModel
{
	const int DefaultPageSize = ContinuationRequest.DefaultMaxRecords;

	[BindProperty(SupportsGet = true)]
	public string AggregateType { get; set; } = "order";

	[BindProperty(SupportsGet = true)]
	public string? AggregateId { get; set; }

	[BindProperty(SupportsGet = true)]
	public int? FromVersion { get; set; }

	[BindProperty(SupportsGet = true)]
	public int? ToVersion { get; set; }

	[BindProperty(SupportsGet = true)]
	public DateTimeOffset? FromUtc { get; set; }

	[BindProperty(SupportsGet = true)]
	public DateTimeOffset? ToUtc { get; set; }

	[BindProperty(SupportsGet = true)]
	public int PageSize { get; set; } = DefaultPageSize;

	[BindProperty(SupportsGet = true)]
	public string? ContinuationToken { get; set; }

	public IReadOnlyList<AggregateEventHistoryItem> Events { get; private set; } = [];

	public string? NextContinuationToken { get; private set; }

	public bool HasQuery => !string.IsNullOrWhiteSpace(AggregateId) || FromUtc.HasValue || ToUtc.HasValue;

	public bool IsRecentMode { get; private set; }

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
	public IReadOnlyList<string> SupportedTypes => AggregateAuditService.SupportedAggregateTypes;

	public async Task<IActionResult> OnGetAsync()
	{
		if (!AggregateAuditService.IsSupportedAggregateType(AggregateType))
		{
			TempData["Error"] = $"Unsupported aggregate type '{AggregateType}'.";
			return Page();
		}

		if (PageSize < 1 || PageSize > 1000)
			PageSize = DefaultPageSize;

		AggregateId = AggregateId?.Trim();
		ResolveDateFiltersFromQuery();
		if (!HasQuery)
			IsRecentMode = true;

		if (!string.IsNullOrWhiteSpace(AggregateId))
		{
			var response = await auditService.GetHistoryAsync(
				AggregateType,
				AggregateId!,
				new AggregateEventHistoryRequest
				{
					FromVersion = FromVersion,
					ToVersion = ToVersion,
					FromUtc = FromUtc,
					ToUtc = ToUtc,
					MaxRecords = PageSize,
					ContinuationToken = ContinuationToken,
				},
				HttpContext.RequestAborted
			);

			Events = response.Results;
			NextContinuationToken = response.ContinuationToken;
			return Page();
		}

		void ResolveDateFiltersFromQuery()
		{
			if (FromUtc.HasValue && ToUtc.HasValue)
				return;

			var query = HttpContext.Request.Query;
			if (!FromUtc.HasValue && TryParseDateTimeLocal(query["fromUtc"], out var fromUtc))
				FromUtc = fromUtc;
			if (!ToUtc.HasValue && TryParseDateTimeLocal(query["toUtc"], out var toUtc))
				ToUtc = toUtc;
		}

		static bool TryParseDateTimeLocal(string? value, out DateTimeOffset parsed)
		{
			if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
				return true;

			if (
				DateTime.TryParse(
					value,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeLocal,
					out var localDateTime
				)
			)
			{
				parsed = new DateTimeOffset(localDateTime);
				return true;
			}

			parsed = default;
			return false;
		}

		IsRecentMode = true;
		Events = await LoadRecentEventsAsync(HttpContext.RequestAborted);
		return Page();
	}

	async Task<IReadOnlyList<AggregateEventHistoryItem>> LoadRecentEventsAsync(CancellationToken cancellationToken)
	{
		var request = new AggregateEventHistoryRequest
		{
			FromUtc = FromUtc,
			ToUtc = ToUtc,
			MaxRecords = PageSize,
		};

		List<AggregateEventHistoryItem> merged = [];
		foreach (var aggregateType in SupportedTypes)
		{
			var results = await auditService.GetLatestHistoryAsync(aggregateType, request, cancellationToken);
			if (results.Count > 0)
				merged.AddRange(results);
		}

		return [.. merged.OrderByDescending(m => m.When).ThenByDescending(m => m.AggregateVersion).Take(PageSize)];
	}

	public string BuildContinuationLink(string continuationToken)
	{
		var query = new QueryBuilder
		{
			{ "aggregateType", AggregateType },
			{ "aggregateId", AggregateId ?? string.Empty },
			{ "pageSize", PageSize.ToString(CultureInfo.InvariantCulture) },
			{ "continuationToken", continuationToken },
		};

		if (FromVersion.HasValue)
			query.Add("fromVersion", FromVersion.Value.ToString(CultureInfo.InvariantCulture));
		if (ToVersion.HasValue)
			query.Add("toVersion", ToVersion.Value.ToString(CultureInfo.InvariantCulture));
		if (FromUtc.HasValue)
			query.Add("fromUtc", FromUtc.Value.ToString("O", CultureInfo.InvariantCulture));
		if (ToUtc.HasValue)
			query.Add("toUtc", ToUtc.Value.ToString("O", CultureInfo.InvariantCulture));

		return $"{HttpContext.Request.PathBase}{HttpContext.Request.Path}{query.ToQueryString()}";
	}
}
