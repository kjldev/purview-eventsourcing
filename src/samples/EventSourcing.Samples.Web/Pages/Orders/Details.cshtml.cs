using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class DetailsModel(IQueryableEventStore<OrderAggregate> store) : PageModel
{
	public OrderAggregate? Order { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Order = await store.GetAsync(id, null, HttpContext.RequestAborted);
	}
}
