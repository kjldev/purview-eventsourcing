using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Customers;

public sealed class DeletedModel(IQueryableEventStore<CustomerAggregate> store) : PageModel
{
	public IReadOnlyList<CustomerAggregate> DeletedCustomers { get; private set; } = [];

	public async Task OnGetAsync()
	{
		var deleted = new List<CustomerAggregate>();
		await foreach (var id in store.GetAggregateIdsAsync(includeDeleted: true, HttpContext.RequestAborted))
		{
			if (await store.IsDeletedAsync(id, HttpContext.RequestAborted))
			{
				var aggregate = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
				if (aggregate != null)
					deleted.Add(aggregate);
			}
		}

		DeletedCustomers = deleted.OrderBy(c => c.Name).ToList();
	}

	public async Task<IActionResult> OnPostRestoreAsync(string id)
	{
		var customer = await store.GetDeletedAsync(id, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		await store.RestoreAsync(customer, null, HttpContext.RequestAborted);

		TempData["Success"] = $"Customer '{customer.Name}' restored.";
		return RedirectToPage("Deleted");
	}
}
