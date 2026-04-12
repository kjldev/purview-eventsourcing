using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class EditModel(IQueryableEventStore<OrderAggregate> store) : PageModel
{
	public OrderAggregate? Order { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Order = await store.GetAsync(id, null, HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostAddLineItemAsync(string id, string productId, string productName, int quantity, decimal unitPrice)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.AddLineItem(productId.Trim(), productName.Trim(), quantity, unitPrice);
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = $"Line item '{productName}' added.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostRemoveLineItemAsync(string id, string productId)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.RemoveLineItem(productId);
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Line item removed.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostSetShippingAddressAsync(string id, string address)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.SetShippingAddress(address.Trim());
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Shipping address updated.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostUpdateNotesAsync(string id, string? notes)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.UpdateNotes(string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Notes updated.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostConfirmAsync(string id)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.ConfirmOrder();
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order confirmed.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostShipAsync(string id)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.ShipOrder();
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order shipped.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostCompleteAsync(string id)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.CompleteOrder();
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order completed.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostCancelAsync(string id)
	{
		var order = await store.GetAsync(id, null, HttpContext.RequestAborted);
		if (order == null) return NotFound();

		order.CancelOrder();
		await store.SaveAsync(order, null, HttpContext.RequestAborted);

		TempData["Success"] = "Order cancelled.";
		return RedirectToPage(new { id });
	}
}
