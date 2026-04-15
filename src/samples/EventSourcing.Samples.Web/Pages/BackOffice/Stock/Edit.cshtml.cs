using Microsoft.AspNetCore.Mvc;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice.Stock;

public sealed class EditModel(IQueryableEventStore store) : EventSourcingPageModel
{
	public InventoryAggregate? Item { get; private set; }

	public async Task OnGetAsync(string id)
	{
		Item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
	}

	public async Task<IActionResult> OnPostReceiveStockAsync(string id, int quantity)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null)
			return NotFound();

		return await TrySaveAsync(
			async () =>
			{
				item.ReceiveStock(quantity);
				await store.SaveAsync(item, null, HttpContext.RequestAborted);
			},
			$"Received {quantity} unit(s).",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostAdjustStockAsync(string id, int newQuantity, string reason)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null)
			return NotFound();

		return await TrySaveAsync(
			async () =>
			{
				item.AdjustStock(newQuantity, reason.Trim());
				await store.SaveAsync(item, null, HttpContext.RequestAborted);
			},
			$"Stock adjusted to {newQuantity}.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostReserveStockAsync(string id, int quantity, string orderId)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null)
			return NotFound();

		return await TrySaveAsync(
			async () =>
			{
				item.ReserveStock(quantity, orderId.Trim());
				await store.SaveAsync(item, null, HttpContext.RequestAborted);
			},
			$"Reserved {quantity} unit(s) for order {orderId}.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostReleaseReservationAsync(string id, int quantity, string orderId)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null)
			return NotFound();

		return await TrySaveAsync(
			async () =>
			{
				item.ReleaseReservation(quantity, orderId.Trim());
				await store.SaveAsync(item, null, HttpContext.RequestAborted);
			},
			$"Released {quantity} unit(s) for order {orderId}.",
			RedirectToPage(new { id })
		);
	}

	public async Task<IActionResult> OnPostShipStockAsync(string id, int quantity, string orderId)
	{
		var item = await store.GetAsync<InventoryAggregate>(id, null, HttpContext.RequestAborted);
		if (item == null)
			return NotFound();

		return await TrySaveAsync(
			async () =>
			{
				item.ShipStock(quantity, orderId.Trim());
				await store.SaveAsync(item, null, HttpContext.RequestAborted);
			},
			$"Shipped {quantity} unit(s) for order {orderId}.",
			RedirectToPage(new { id })
		);
	}
}
