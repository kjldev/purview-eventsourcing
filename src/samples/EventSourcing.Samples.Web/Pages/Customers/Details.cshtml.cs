using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Customers;

public sealed class DetailsModel(IQueryableEventStore<CustomerAggregate> store) : PageModel
{
	public CustomerAggregate? Customer { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
	}
}
