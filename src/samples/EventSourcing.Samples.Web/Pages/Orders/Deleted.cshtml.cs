using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class DeletedModel(IQueryableEventStore<OrderAggregate> store) : PageModel
{
	public IReadOnlyList<OrderAggregate> DeletedOrders { get; private set; } = [];

	public async Task OnGetAsync()
	{
		var deleted = new List<OrderAggregate>();
		await foreach (var id in store.GetAggregateIdsAsync(includeDeleted: true, HttpContext.RequestAborted))
		{
			if (await store.IsDeletedAsync(id, HttpContext.RequestAborted))
			{
				var aggregate = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
				if (aggregate != null)
					deleted.Add(aggregate);
			}
		}

		DeletedOrders = deleted;
	}

	public async Task<IActionResult> OnPostRestoreAsync(string id)
	{
		var order = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		await store.RestoreAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order restored.";
		return RedirectToPage("Deleted");
	}
}
