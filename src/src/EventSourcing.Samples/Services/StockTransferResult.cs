using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class StockTransferResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public InventoryAggregate? Source { get; private init; }
    public InventoryAggregate? Destination { get; private init; }
    public int Quantity { get; private init; }

    public static StockTransferResult Success(
        InventoryAggregate source,
        InventoryAggregate destination,
        int quantity
    ) =>
        new()
        {
            Succeeded = true,
            Source = source,
            Destination = destination,
            Quantity = quantity,
        };

    public static StockTransferResult Fail(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
