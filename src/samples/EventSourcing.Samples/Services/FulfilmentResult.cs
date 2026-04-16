using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class FulfilmentResult
{
	public bool Succeeded { get; private init; }
	public string? ErrorMessage { get; private init; }
	public OrderAggregate? Order { get; private init; }
	public InventoryAggregate? Inventory { get; private init; }

	public static FulfilmentResult Success(OrderAggregate order, InventoryAggregate inventory) =>
		new()
		{
			Succeeded = true,
			Order = order,
			Inventory = inventory,
		};

	public static FulfilmentResult Fail(string message) => new() { Succeeded = false, ErrorMessage = message };
}
