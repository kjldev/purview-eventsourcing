using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Customer;

sealed class IndexModel(IQueryableEventStore store) : PageModel
{
	const int DefaultPageSize = 15;

	[BindProperty(SupportsGet = true)]
	public string? Search { get; set; }

	[BindProperty(SupportsGet = true)]
	public bool ShowInactive { get; set; }

	[BindProperty(SupportsGet = true)]
	public new int Page { get; set; } = 1;

	[BindProperty(SupportsGet = true)]
	public int PageSize { get; set; } = DefaultPageSize;

	public IReadOnlyList<CustomerAggregate> Customers { get; private set; } = [];
	public long TotalCount { get; private set; }
	public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
	public bool HasPrevPage => Page > 1;
	public bool HasNextPage => Page < TotalPages;

	public async Task OnGetAsync()
	{
		if (Page < 1)
			Page = 1;
		if (PageSize < 5 || PageSize > 100)
			PageSize = DefaultPageSize;

		var ct = HttpContext.RequestAborted;
		var skipCount = (Page - 1) * PageSize;
		var request = new ContinuationRequest
		{
			ContinuationToken = skipCount > 0 ? skipCount.ToString() : null,
			MaxRecords = PageSize,
		};

		var search = Search?.Trim();
		var hasFilter = !string.IsNullOrEmpty(search) || !ShowInactive;

		Expression<Func<CustomerAggregate, bool>> where = hasFilter
			? c =>
				(
					string.IsNullOrEmpty(search)
					|| EF.Functions.Contains(c.Name, search)
					|| EF.Functions.Contains(c.Email, search)
				) && (ShowInactive || c.IsActive)
			: c => true;

		TotalCount = await store.CountAsync(where, ct);

		var result = await store.QueryAsync(where, q => q.OrderBy(c => c.Name), request, ct);
		Customers = result.Results;
	}

	public async Task<IActionResult> OnPostSelectAsync(string id, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(id))
			return RedirectToPage();

		HttpContext.Session.SetString("selectedCustomerId", id);
		await HttpContext.Session.CommitAsync(cancellationToken);
		return RedirectToPage("/Customer/Catalog/Index");
	}

	public string PaginationLink(int page)
	{
		var query = new QueryBuilder
		{
			{ "page", page.ToString(CultureInfo.InvariantCulture) },
			{ "pageSize", PageSize.ToString(CultureInfo.InvariantCulture) },
		};

		if (!string.IsNullOrWhiteSpace(Search))
			query.Add("search", Search);
		if (ShowInactive)
			query.Add("showInactive", bool.TrueString.ToLowerInvariant());

		return $"{HttpContext.Request.PathBase}{HttpContext.Request.Path}{query.ToQueryString()}";
	}
}
