using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class CartCheckoutResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public OrderAggregate? Order { get; private init; }

    public static CartCheckoutResult Success(OrderAggregate order) =>
        new() { Succeeded = true, Order = order };

    public static CartCheckoutResult Fail(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
