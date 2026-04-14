using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Infrastructure;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Catalog;

public sealed class EditModel(
	IQueryableEventStore store,
	IProductImageService imageService
) : EventSourcingPageModel
{
	public InventoryAggregate? Item { get; private set; }
	public string? CurrentImageUrl { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (Item != null)
			CurrentImageUrl = await imageService.GetImageUrlAsync(Item.ProductId, HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostUpdateProductNameAsync(string id, string productName)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null) return NotFound();

		return await TrySaveAsync(
			async () => { item.UpdateDetails(productName: productName.Trim()); await store.SaveAsync(item, null, HttpContext.RequestAborted); },
			"Product name updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostUpdateLocationNameAsync(string id, string locationName)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null) return NotFound();

		return await TrySaveAsync(
			async () => { item.UpdateDetails(locationName: locationName.Trim()); await store.SaveAsync(item, null, HttpContext.RequestAborted); },
			"Location name updated.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostUploadImageAsync(string id, IFormFile? imageFile)
	{
		if (imageFile == null || imageFile.Length == 0)
		{
			TempData["Error"] = "Please select an image file.";
			return RedirectToPage(new { id });
		}

		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null) return NotFound();

		var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
		if (!allowedTypes.Contains(imageFile.ContentType, StringComparer.OrdinalIgnoreCase))
		{
			TempData["Error"] = "Only JPEG, PNG, WebP or GIF images are allowed.";
			return RedirectToPage(new { id });
		}

		await using var stream = imageFile.OpenReadStream();
		await imageService.UploadImageAsync(item.ProductId, stream, imageFile.ContentType, HttpContext.RequestAborted);

		TempData["Success"] = "Image uploaded.";
		return RedirectToPage(new { id });
	}

	public async Task<IActionResult> OnPostDeleteImageAsync(string id)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null) return NotFound();

		await imageService.DeleteImageAsync(item.ProductId, HttpContext.RequestAborted);

		TempData["Success"] = "Image removed.";
		return RedirectToPage(new { id });
	}
}

