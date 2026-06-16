using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Customers;

sealed class DeletedModel(IQueryableEventStore store) : PageModel
{
	public IReadOnlyList<CustomerAggregate> DeletedCustomers { get; private set; } = [];

	public async Task OnGetAsync()
	{
		var deleted = new List<CustomerAggregate>();
		await foreach (
			var id in store.GetAggregateIdsAsync<CustomerAggregate>(includeDeleted: true, HttpContext.RequestAborted)
		)
		{
			if (await store.IsDeletedAsync<CustomerAggregate>(id, HttpContext.RequestAborted))
			{
				var aggregate = await store.GetDeletedAsync<CustomerAggregate>(id, HttpContext.RequestAborted);
				if (aggregate != null)
					deleted.Add(aggregate);
			}
		}

		DeletedCustomers = [.. deleted.OrderBy(c => c.Name)];
	}

	public async Task<IActionResult> OnPostRestoreAsync(string id)
	{
		var customer = await store.GetDeletedAsync<CustomerAggregate>(id, HttpContext.RequestAborted);
		if (customer == null)
			return NotFound();

		await store.RestoreAsync(customer, HttpContext.RequestAborted);

		TempData["Success"] = $"Customer '{customer.Name}' restored.";
		return RedirectToPage("Deleted");
	}
}
