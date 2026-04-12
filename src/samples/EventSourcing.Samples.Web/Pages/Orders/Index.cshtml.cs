using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class IndexModel(IQueryableEventStore<OrderAggregate> store) : PageModel
{
	const int DefaultPageSize = 15;

	[BindProperty(SupportsGet = true)] public string? Search { get; set; }
	[BindProperty(SupportsGet = true)] public OrderStatus? StatusFilter { get; set; }
	[BindProperty(SupportsGet = true)] public new int Page { get; set; } = 1;
	[BindProperty(SupportsGet = true)] public int PageSize { get; set; } = DefaultPageSize;
	[BindProperty(SupportsGet = true)] public string SortBy { get; set; } = "version";
	[BindProperty(SupportsGet = true)] public string SortDir { get; set; } = "desc";

	public IReadOnlyList<OrderAggregate> Orders { get; private set; } = [];
	public long TotalCount { get; private set; }
	public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
	public bool HasPrevPage => Page > 1;
	public bool HasNextPage => Page < TotalPages;

	public async Task OnGetAsync()
	{
		if (Page < 1) Page = 1;
		if (PageSize < 5 || PageSize > 100) PageSize = DefaultPageSize;

		var ct = HttpContext.RequestAborted;
		var skipCount = (Page - 1) * PageSize;
		var request = new ContinuationRequest
		{
			ContinuationToken = skipCount > 0 ? skipCount.ToString() : null,
			MaxRecords = PageSize
		};

		var search = Search?.Trim().ToLowerInvariant() ?? string.Empty;
		var statusFilter = StatusFilter;
		var hasFilter = !string.IsNullOrEmpty(search) || statusFilter.HasValue;

		Expression<Func<OrderAggregate, bool>> where = hasFilter
			? o => (string.IsNullOrEmpty(search) || o.CustomerId.ToLower().Contains(search))
				&& (!statusFilter.HasValue || o.Status == statusFilter.Value)
			: o => true;

		Func<IQueryable<OrderAggregate>, IQueryable<OrderAggregate>> orderBy = (SortBy, SortDir) switch
		{
			("status", "desc") => q => q.OrderByDescending(o => o.Status),
			("status", _) => q => q.OrderBy(o => o.Status),
			("total", "desc") => q => q.OrderByDescending(o => o.TotalAmount),
			("total", _) => q => q.OrderBy(o => o.TotalAmount),
			("items", "desc") => q => q.OrderByDescending(o => o.LineItems.Length),
			("items", _) => q => q.OrderBy(o => o.LineItems.Length),
			("version", _) when SortDir == "asc" => q => q.OrderBy(o => o.Details.SavedVersion),
			_ => q => q.OrderByDescending(o => o.Details.SavedVersion)
		};

		TotalCount = await store.CountAsync(hasFilter ? where : null, ct);

		var result = hasFilter
			? await store.QueryAsync(where, orderBy, request, ct)
			: await store.ListAsync(orderBy, request, ct);
		Orders = result.Results;
	}

	public async Task<IActionResult> OnPostArchiveAsync(string id)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order != null)
		{
			await store.DeleteAsync(order, null, HttpContext.RequestAborted);
			TempData["Success"] = "Order archived.";
		}

		return RedirectToPage("Index");
	}

	public string SortLink(string column) =>
		column == SortBy && SortDir == "asc" ? "desc" : "asc";

	public string SortIcon(string column) =>
		SortBy != column ? "↕" : SortDir == "asc" ? "↑" : "↓";
}

