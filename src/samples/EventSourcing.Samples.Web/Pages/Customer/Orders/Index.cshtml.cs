using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Customer.Orders;

public sealed class IndexModel(IQueryableEventStore customerStore, IQueryableEventStore orderStore) : PageModel
{
	const int DefaultPageSize = 15;

	[BindProperty(SupportsGet = true)]
	public new int Page { get; set; } = 1;

	[BindProperty(SupportsGet = true)]
	public int PageSize { get; set; } = DefaultPageSize;

	public CustomerAggregate? CurrentCustomer { get; private set; }
	public IReadOnlyList<OrderAggregate> Orders { get; private set; } = [];
	public long TotalCount { get; private set; }
	public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
	public bool HasPrevPage => Page > 1;
	public bool HasNextPage => Page < TotalPages;

	public async Task<IActionResult> OnGetAsync()
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId))
			return RedirectToPage("/Customer/Index");

		if (Page < 1)
			Page = 1;
		if (PageSize < 5 || PageSize > 100)
			PageSize = DefaultPageSize;

		var ct = HttpContext.RequestAborted;
		CurrentCustomer = await customerStore.GetAsync<CustomerAggregate>(customerId, ct);

		var skipCount = (Page - 1) * PageSize;
		var request = new ContinuationRequest
		{
			ContinuationToken = skipCount > 0 ? skipCount.ToString() : null,
			MaxRecords = PageSize,
		};

		Expression<Func<OrderAggregate, bool>> where = o => o.CustomerId == customerId;
		TotalCount = await orderStore.CountAsync<OrderAggregate>(where, ct);

		var result = await orderStore.QueryAsync<OrderAggregate>(
			where,
			q => q.OrderByDescending(o => o.Details.SavedVersion),
			request,
			ct
		);
		Orders = result.Results;

		return Page();
	}
}
