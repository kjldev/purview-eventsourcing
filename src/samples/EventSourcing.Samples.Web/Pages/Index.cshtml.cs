using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages;

public sealed class IndexModel(
	IQueryableEventStore<CustomerAggregate> customers,
	IQueryableEventStore<OrderAggregate> orders,
	IQueryableEventStore<InventoryAggregate> inventory
) : PageModel
{
	public long CustomerCount { get; private set; }
	public long OrderCount { get; private set; }
	public long InventoryCount { get; private set; }

	public async Task OnGetAsync()
	{
		var ct = HttpContext.RequestAborted;
		CustomerCount = await customers.CountAsync(null, ct);
		OrderCount = await orders.CountAsync(null, ct);
		InventoryCount = await inventory.CountAsync(null, ct);
	}
}
