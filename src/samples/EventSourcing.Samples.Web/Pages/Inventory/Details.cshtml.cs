using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Inventory;

public sealed class DetailsModel(IQueryableEventStore<InventoryAggregate> store) : PageModel
{
	public InventoryAggregate? Item { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Item = await store.GetAsync(id, null, HttpContext.RequestAborted);
	}
}
