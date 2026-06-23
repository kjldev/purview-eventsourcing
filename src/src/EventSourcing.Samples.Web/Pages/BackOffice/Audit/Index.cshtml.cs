using System.Globalization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Audit;

sealed class IndexModel(AggregateAuditService auditService) : PageModel
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

	public bool HasQuery => !string.IsNullOrWhiteSpace(AggregateId);

	public IReadOnlyList<string> SupportedTypes => AggregateAuditService.SupportedAggregateTypes;

	public async Task<IActionResult> OnGetAsync()
	{
		if (!HasQuery)
			return Page();

		if (!AggregateAuditService.IsSupportedAggregateType(AggregateType))
		{
			TempData["Error"] = $"Unsupported aggregate type '{AggregateType}'.";
			return Page();
		}

		if (PageSize < 1 || PageSize > 1000)
			PageSize = DefaultPageSize;

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
