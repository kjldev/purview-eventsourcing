namespace Purview.EventSourcing.Samples.Services;

public sealed record CartItem(
	string ProductId,
	string ProductName,
	string InventoryId,
	int Quantity,
	decimal UnitPrice);
