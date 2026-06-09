using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Stock;

sealed class IndexModel(IQueryableEventStore store) : PageModel
{
	const int DefaultPageSize = 15;

	[BindProperty(SupportsGet = true)]
	public string? Search { get; set; }

	[BindProperty(SupportsGet = true)]
	public new int Page { get; set; } = 1;

	[BindProperty(SupportsGet = true)]
	public int PageSize { get; set; } = DefaultPageSize;

	[BindProperty(SupportsGet = true)]
	public string SortBy { get; set; } = "available";

	[BindProperty(SupportsGet = true)]
	public string SortDir { get; set; } = "desc";

	public IReadOnlyList<InventoryAggregate> Items { get; private set; } = [];

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
		var hasFilter = !string.IsNullOrEmpty(search);

		Expression<Func<InventoryAggregate, bool>> where = i =>
			i.ProductId.ToLower().Contains(search) || i.ProductName.ToLower().Contains(search);

		Func<IQueryable<InventoryAggregate>, IQueryable<InventoryAggregate>> orderBy = (SortBy, SortDir) switch
		{
			("onhand", "asc") => q => q.OrderBy(i => i.QuantityOnHand),
			("onhand", _) => q => q.OrderByDescending(i => i.QuantityOnHand),
			("reserved", "asc") => q => q.OrderBy(i => i.ReservedQuantity),
			("reserved", _) => q => q.OrderByDescending(i => i.ReservedQuantity),
			("name", "asc") => q => q.OrderBy(i => i.ProductName),
			("name", _) => q => q.OrderByDescending(i => i.ProductName),
			("available", "asc") => q => q.OrderBy(i => i.AvailableQuantity),
			_ => q => q.OrderByDescending(i => i.AvailableQuantity),
		};

		TotalCount = await store.CountAsync(hasFilter ? where : null, ct);

		var result = hasFilter
			? await store.QueryAsync(where, orderBy, request, ct)
			: await store.ListAsync(orderBy, request, ct);
		Items = result.Results;
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

		return $"{HttpContext.Request.PathBase}{HttpContext.Request.Path}{query.ToQueryString()}";
	}

	public string SortLink(string column) => column == SortBy && SortDir == "asc" ? "desc" : "asc";

	public string SortIcon(string column) =>
		SortBy != column ? "↕"
		: SortDir == "asc" ? "↑"
		: "↓";
}
