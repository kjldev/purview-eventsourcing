using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.Inventory;

public sealed class CreateModel(IQueryableEventStore<InventoryAggregate> store) : PageModel
{
	[BindProperty, Required, MaxLength(200)]
	public string ProductId { get; set; } = string.Empty;

	[BindProperty, Required, MaxLength(500)]
	public string ProductName { get; set; } = string.Empty;

	[BindProperty, Range(0, int.MaxValue)]
	public int InitialQuantity { get; set; }

	public async Task<IActionResult> OnPostAsync()
	{
		if (!ModelState.IsValid)
			return Page();

		var item = await store.CreateAsync(cancellationToken: HttpContext.RequestAborted);
		item.Initialize(ProductId.Trim(), ProductName.Trim(), InitialQuantity);
		await store.SaveAsync(item, null, HttpContext.RequestAborted);

		TempData["Success"] = $"Inventory item '{item.ProductName}' initialized.";
		return RedirectToPage("Index");
	}
}
