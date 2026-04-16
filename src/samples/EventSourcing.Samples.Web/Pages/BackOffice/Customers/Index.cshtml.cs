using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Customers;

public sealed class IndexModel(IQueryableEventStore store) : PageModel
{
	const int DefaultPageSize = 15;

	[BindProperty(SupportsGet = true)]
	public string? Search { get; set; }

	[BindProperty(SupportsGet = true)]
	public bool? ActiveFilter { get; set; }

	[BindProperty(SupportsGet = true)]
	public new int Page { get; set; } = 1;

	[BindProperty(SupportsGet = true)]
	public int PageSize { get; set; } = DefaultPageSize;

	[BindProperty(SupportsGet = true)]
	public string SortBy { get; set; } = "name";

	[BindProperty(SupportsGet = true)]
	public string SortDir { get; set; } = "asc";

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

		var search = Search?.Trim().ToLowerInvariant() ?? string.Empty;
		var activeFilter = ActiveFilter;
		var hasFilter = !string.IsNullOrEmpty(search) || activeFilter.HasValue;

		Expression<Func<CustomerAggregate, bool>> where = hasFilter
			? c =>
				(
					string.IsNullOrEmpty(search)
					|| c.Name.ToLower().Contains(search)
					|| c.Email.ToLower().Contains(search)
				) && (!activeFilter.HasValue || c.IsActive == activeFilter.Value)
			: c => true;

		Func<IQueryable<CustomerAggregate>, IQueryable<CustomerAggregate>> orderBy = (SortBy, SortDir) switch
		{
			("email", "desc") => q => q.OrderByDescending(c => c.Email),
			("email", _) => q => q.OrderBy(c => c.Email),
			("status", "desc") => q => q.OrderByDescending(c => c.IsActive),
			("status", _) => q => q.OrderBy(c => c.IsActive),
			("name", "desc") => q => q.OrderByDescending(c => c.Name),
			_ => q => q.OrderBy(c => c.Name),
		};

		TotalCount = await store.CountAsync<CustomerAggregate>(hasFilter ? where : null, ct);

		var result = hasFilter
			? await store.QueryAsync<CustomerAggregate>(where, orderBy, request, ct)
			: await store.ListAsync<CustomerAggregate>(orderBy, request, ct);
		Customers = result.Results;
	}

	public async Task<IActionResult> OnPostArchiveAsync(string id)
	{
		var customer = await store.GetAsync<CustomerAggregate>(id, HttpContext.RequestAborted);
		if (customer != null)
		{
			await store.DeleteAsync(customer, HttpContext.RequestAborted);
			TempData["Success"] = $"Customer '{customer.Name}' archived.";
		}

		return RedirectToPage("Index");
	}

	public string PaginationLink(int page)
	{
		var query = new QueryBuilder
		{
			{ "sortBy", SortBy },
			{ "sortDir", SortDir },
			{ "page", page.ToString(CultureInfo.InvariantCulture) },
			{ "pageSize", PageSize.ToString(CultureInfo.InvariantCulture) },
		};

		if (!string.IsNullOrWhiteSpace(Search))
			query.Add("search", Search);
		if (ActiveFilter.HasValue)
			query.Add("activeFilter", ActiveFilter.Value.ToString().ToLowerInvariant());

		return $"{HttpContext.Request.PathBase}{HttpContext.Request.Path}{query.ToQueryString()}";
	}

	public string SortLink(string column) => column == SortBy && SortDir == "asc" ? "desc" : "asc";

	public string SortIcon(string column) =>
		SortBy != column ? "↕"
		: SortDir == "asc" ? "↑"
		: "↓";
}
