using Microsoft.AspNetCore.Mvc.RazorPages;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Pages.BackOffice;

public sealed class IndexModel(
	IQueryableEventStore inventoryStore,
	IQueryableEventStore orderStore
) : PageModel
{
	public int UniqueProductCount { get; private set; }
	public long InventoryItemCount { get; private set; }
	public int TotalAvailableStock { get; private set; }
	public long DraftOrderCount { get; private set; }
	public long ActiveOrderCount { get; private set; }
	public long CompletedOrderCount { get; private set; }

	public async Task OnGetAsync()
	{
		var ct = HttpContext.RequestAborted;

		var request = new ContinuationRequest { MaxRecords = 1000 };
		var inventoryResult = await inventoryStore.ListAsync<InventoryAggregate>(
			q => q.OrderBy(i => i.ProductId),
			request,
			ct
		);
		var allItems = inventoryResult.Results;

		UniqueProductCount = allItems.Select(i => i.ProductId).Distinct().Count();
		InventoryItemCount = await inventoryStore.CountAsync<InventoryAggregate>(null, ct);
		TotalAvailableStock = allItems.Sum(i => i.AvailableQuantity);

		DraftOrderCount = await orderStore.CountAsync<OrderAggregate>(o => o.Status == OrderStatus.Draft, ct);
		ActiveOrderCount = await orderStore.CountAsync<OrderAggregate>(
			o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped,
			ct
		);
		CompletedOrderCount = await orderStore.CountAsync<OrderAggregate>(
			o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Cancelled,
			ct
		);
	}
}
