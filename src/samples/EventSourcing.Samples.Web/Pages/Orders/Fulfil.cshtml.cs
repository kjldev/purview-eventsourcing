using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.SqlServer.Exceptions;

namespace Purview.EventSourcing.Samples.Web.Pages.Orders;

public sealed class FulfilModel(
	IOrderFulfillmentService fulfillmentService,
	IQueryableEventStore<CustomerAggregate> customerStore,
	IQueryableEventStore<InventoryAggregate> inventoryStore
) : PageModel
{
	[BindProperty] public string CustomerId { get; set; } = string.Empty;
	[BindProperty] public string InventoryId { get; set; } = string.Empty;
	[BindProperty] public int Quantity { get; set; } = 1;
	[BindProperty] public string? ShippingAddress { get; set; }

	public IReadOnlyList<SelectListItem> Customers { get; private set; } = [];
	public IReadOnlyList<SelectListItem> InventoryItems { get; private set; } = [];

	public async Task OnGetAsync()
	{
		await LoadDropdownsAsync(HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostAsync()
	{
		var ct = HttpContext.RequestAborted;

		if (!ModelState.IsValid)
		{
			await LoadDropdownsAsync(ct);
			return Page();
		}

		if (Quantity < 1)
		{
			ModelState.AddModelError(nameof(Quantity), "Quantity must be at least 1.");
			await LoadDropdownsAsync(ct);
			return Page();
		}

		FulfilmentResult result;
		try
		{
			result = await fulfillmentService.PlaceOrderAsync(
				CustomerId, InventoryId, Quantity, ShippingAddress, ct
			);
		}
		catch (ConcurrencyException)
		{
			ModelState.AddModelError(string.Empty,
				"Another user modified the order or inventory concurrently. Please try again.");
			await LoadDropdownsAsync(ct);
			return Page();
		}

		if (!result.Succeeded)
		{
			ModelState.AddModelError(string.Empty, result.ErrorMessage!);
			await LoadDropdownsAsync(ct);
			return Page();
		}
		var orderId = result.Order!.Id();
		TempData["Success"] = $"Order {(orderId.Length >= 8 ? orderId[..8] : orderId)}… placed and inventory reserved. Total: {result.Order.TotalAmount:C}";
		return RedirectToPage("Details", new { id = result.Order.Id() });
	}

	async Task LoadDropdownsAsync(CancellationToken ct)
	{
		var request = new ContinuationRequest { MaxRecords = 500 };

		var customersResult = await customerStore.QueryAsync(
			c => c.IsActive,
			q => q.OrderBy(c => c.Name),
			request, ct
		);
		Customers = customersResult.Results
			.Select(c => new SelectListItem($"{c.Name} ({c.Email})", c.Id()))
			.ToList();

		var inventoryResult = await inventoryStore.QueryAsync(
			i => i.AvailableQuantity > 0,
			q => q.OrderBy(i => i.ProductName),
			request, ct
		);
		InventoryItems = inventoryResult.Results
			.Select(i => new SelectListItem($"{i.ProductName} — {i.AvailableQuantity} available", i.Id()))
			.ToList();
	}
}
