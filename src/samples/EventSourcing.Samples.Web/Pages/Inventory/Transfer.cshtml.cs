using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.SqlServer.Exceptions;

namespace Purview.EventSourcing.Samples.Web.Pages.Inventory;

public sealed class TransferModel(
	IStockTransferService transferService,
	IQueryableEventStore<InventoryAggregate> inventoryStore
) : PageModel
{
	[BindProperty] public string SourceId { get; set; } = string.Empty;
	[BindProperty] public string DestinationId { get; set; } = string.Empty;
	[BindProperty] public int Quantity { get; set; } = 1;
	[BindProperty] public string Reason { get; set; } = string.Empty;

	public IReadOnlyList<SelectListItem> InventoryItems { get; private set; } = [];

	public async Task OnGetAsync()
	{
		await LoadDropdownsAsync(HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostAsync()
	{
		var ct = HttpContext.RequestAborted;

		if (string.IsNullOrWhiteSpace(SourceId))
			ModelState.AddModelError(nameof(SourceId), "Please select a source item.");
		if (string.IsNullOrWhiteSpace(DestinationId))
			ModelState.AddModelError(nameof(DestinationId), "Please select a destination item.");
		if (!string.IsNullOrWhiteSpace(SourceId) && SourceId == DestinationId)
			ModelState.AddModelError(nameof(DestinationId), "Source and destination must be different items.");
		if (Quantity < 1)
			ModelState.AddModelError(nameof(Quantity), "Quantity must be at least 1.");
		if (string.IsNullOrWhiteSpace(Reason))
			ModelState.AddModelError(nameof(Reason), "A reason for the transfer is required.");

		if (!ModelState.IsValid)
		{
			await LoadDropdownsAsync(ct);
			return Page();
		}

		StockTransferResult result;
		try
		{
			result = await transferService.TransferAsync(SourceId, DestinationId, Quantity, Reason.Trim(), ct);
		}
		catch (ConcurrencyException)
		{
			ModelState.AddModelError(string.Empty,
				"Another user modified the inventory concurrently. Please refresh and try again.");
			await LoadDropdownsAsync(ct);
			return Page();
		}

		if (!result.Succeeded)
		{
			ModelState.AddModelError(string.Empty, result.ErrorMessage!);
			await LoadDropdownsAsync(ct);
			return Page();
		}

		TempData["Success"] =
			$"Transferred {result.Quantity} unit(s) from '{result.Source!.ProductName}' " +
			$"to '{result.Destination!.ProductName}'.";
		return RedirectToPage("Index");
	}

	async Task LoadDropdownsAsync(CancellationToken ct)
	{
		var request = new ContinuationRequest { MaxRecords = 500 };
		var result = await inventoryStore.QueryAsync(
			_ => true,
			q => q.OrderBy(i => i.ProductName),
			request, ct
		);
		InventoryItems = result.Results
			.Select(i => new SelectListItem(
				$"{i.ProductName} (on hand: {i.QuantityOnHand}, available: {i.AvailableQuantity})",
				i.Id()
			))
			.ToList();
	}
}
