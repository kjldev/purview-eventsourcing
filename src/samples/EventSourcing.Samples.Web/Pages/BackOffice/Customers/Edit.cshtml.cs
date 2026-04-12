using Microsoft.AspNetCore.Mvc;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Customers;

public sealed class EditModel(IQueryableEventStore<CustomerAggregate> store) : EventSourcingPageModel
{
	public CustomerAggregate? Customer { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostChangeNameAsync(string id, string newName)
	{
		if (string.IsNullOrWhiteSpace(newName)) { TempData["Error"] = "Name cannot be empty."; return RedirectToPage(new { id }); }

		var customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		return await TrySaveAsync(
			async () => { customer.ChangeName(newName.Trim()); await store.SaveAsync(customer, null, HttpContext.RequestAborted); },
			"Name updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostChangeEmailAsync(string id, string newEmail)
	{
		var customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		return await TrySaveAsync(
			async () => { customer.ChangeEmail(newEmail.Trim().ToLowerInvariant()); await store.SaveAsync(customer, null, HttpContext.RequestAborted); },
			"Email updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostChangePhoneAsync(string id, string? phoneNumber)
	{
		var customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		return await TrySaveAsync(
			async () => { customer.ChangePhoneNumber(string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim()); await store.SaveAsync(customer, null, HttpContext.RequestAborted); },
			"Phone number updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostDeactivateAsync(string id)
	{
		var customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		return await TrySaveAsync(
			async () => { customer.Deactivate(); await store.SaveAsync(customer, null, HttpContext.RequestAborted); },
			"Customer deactivated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostReactivateAsync(string id)
	{
		var customer = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (customer == null) return NotFound();

		return await TrySaveAsync(
			async () => { customer.Reactivate(); await store.SaveAsync(customer, null, HttpContext.RequestAborted); },
			"Customer reactivated.",
			RedirectToPage(new { id })
		);
	}
}
