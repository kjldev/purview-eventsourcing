using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Catalog;

public sealed class DeletedModel(IQueryableEventStore<InventoryAggregate> store) : PageModel
{
	public IReadOnlyList<InventoryAggregate> DeletedItems { get; private set; } = [];

	public async Task OnGetAsync()
	{
		var deleted = new List<InventoryAggregate>();
		await foreach (var id in store.GetAggregateIdsAsync(includeDeleted: true, HttpContext.RequestAborted))
		{
			if (await store.IsDeletedAsync(id, HttpContext.RequestAborted))
			{
				var aggregate = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
				if (aggregate != null)
					deleted.Add(aggregate);
			}
		}

		DeletedItems = deleted.OrderBy(i => i.ProductName).ToList();
	}

	public async Task<IActionResult> OnPostRestoreAsync(string id)
	{
		var item = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
		if (item == null) return NotFound();

		await store.RestoreAsync(item, null, HttpContext.RequestAborted);

		TempData["Success"] = $"'{item.ProductName}' restored.";
		return RedirectToPage("Deleted");
	}
}
