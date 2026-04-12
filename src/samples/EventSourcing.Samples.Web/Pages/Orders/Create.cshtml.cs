using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class CreateModel(IQueryableEventStore<OrderAggregate> store) : PageModel
{
	[BindProperty, Required, MaxLength(500)]
	public string CustomerId { get; set; } = string.Empty;

	public async Task<IActionResult> OnPostAsync()
	{
		if (!ModelState.IsValid)
			return Page();

		var order = await store.CreateAsync(cancellationToken: HttpContext.RequestAborted);
		order.CreateOrder(CustomerId.Trim());
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order created.";
		return RedirectToPage("Edit", new { id = order.Id() });
	}
}
