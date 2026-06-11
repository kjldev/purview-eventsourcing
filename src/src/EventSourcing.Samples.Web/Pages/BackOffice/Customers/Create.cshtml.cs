using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Customers;

sealed class CreateModel(IQueryableEventStore store) : PageModel
{
	[BindProperty, Required, MaxLength(200)]
	public string Name { get; set; } = string.Empty;

	[BindProperty, Required, EmailAddress, MaxLength(300)]
	public string Email { get; set; } = string.Empty;

	public async Task<IActionResult> OnPostAsync()
	{
		if (!ModelState.IsValid)
			return Page();

		var customer = await store.CreateAsync<CustomerAggregate>(cancellationToken: HttpContext.RequestAborted);
		customer.RegisterCustomer(Name, Email);
		await store.SaveAsync(customer, HttpContext.RequestAborted);

		TempData["Success"] = $"Customer '{customer.Name}' registered.";
		return RedirectToPage("Index");
	}
}
