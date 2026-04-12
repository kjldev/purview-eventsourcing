using Microsoft.AspNetCore.Mvc;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.Pages.Customer.Orders;

public sealed class DetailsModel(
	IQueryableEventStore<CustomerAggregate> customerStore,
	IQueryableEventStore<OrderAggregate> orderStore
) : EventSourcingPageModel
{
	public OrderAggregate? Order { get; private set; }
	public CustomerAggregate? CurrentCustomer { get; private set; }

	public async Task<IActionResult> OnGetAsync(string id)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId))
			return RedirectToPage("/Customer/Index");

		var ct = HttpContext.RequestAborted;
		CurrentCustomer = await customerStore.GetAsync(customerId, null, ct);
		Order = await orderStore.GetAsync(id, null, ct);

		if (Order == null || Order.CustomerId != customerId)
			return NotFound();

		return Page();
	}

	public async Task<IActionResult> OnPostAddLineItemAsync(string id, string productId, string productName, int quantity, decimal unitPrice)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId)) return RedirectToPage("/Customer/Index");

		var order = await orderStore.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null || order.CustomerId != customerId) return NotFound();
		if (order.Status != OrderStatus.Draft) { TempData["Error"] = "Only draft orders can be edited."; return RedirectToPage(new { id }); }

		return await TrySaveAsync(
			async () => { order.AddLineItem(productId.Trim(), productName.Trim(), quantity, unitPrice); await orderStore.SaveAsync(order, null, HttpContext.RequestAborted); },
			"Line item added.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostRemoveLineItemAsync(string id, string productId)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId)) return RedirectToPage("/Customer/Index");

		var order = await orderStore.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null || order.CustomerId != customerId) return NotFound();
		if (order.Status != OrderStatus.Draft) { TempData["Error"] = "Only draft orders can be edited."; return RedirectToPage(new { id }); }

		return await TrySaveAsync(
			async () => { order.RemoveLineItem(productId); await orderStore.SaveAsync(order, null, HttpContext.RequestAborted); },
			"Line item removed.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostSetShippingAddressAsync(string id, string address)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId)) return RedirectToPage("/Customer/Index");

		var order = await orderStore.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null || order.CustomerId != customerId) return NotFound();
		if (order.Status != OrderStatus.Draft) { TempData["Error"] = "Only draft orders can be edited."; return RedirectToPage(new { id }); }

		return await TrySaveAsync(
			async () => { order.SetShippingAddress(address.Trim()); await orderStore.SaveAsync(order, null, HttpContext.RequestAborted); },
			"Shipping address updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostUpdateNotesAsync(string id, string? notes)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId)) return RedirectToPage("/Customer/Index");

		var order = await orderStore.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null || order.CustomerId != customerId) return NotFound();
		if (order.Status != OrderStatus.Draft) { TempData["Error"] = "Only draft orders can be edited."; return RedirectToPage(new { id }); }

		return await TrySaveAsync(
			async () => { order.UpdateNotes(string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()); await orderStore.SaveAsync(order, null, HttpContext.RequestAborted); },
			"Notes updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostCancelAsync(string id)
	{
		var customerId = HttpContext.Session.GetString("selectedCustomerId");
		if (string.IsNullOrEmpty(customerId)) return RedirectToPage("/Customer/Index");

		var order = await orderStore.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null || order.CustomerId != customerId) return NotFound();
		if (order.Status != OrderStatus.Draft) { TempData["Error"] = "Only draft orders can be cancelled by customers."; return RedirectToPage(new { id }); }

		return await TrySaveAsync(
			async () => { order.CancelOrder(); await orderStore.SaveAsync(order, null, HttpContext.RequestAborted); },
			"Order cancelled.",
			RedirectToPage(new { id })
		);
	}
}
