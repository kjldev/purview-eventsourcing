namespace Purview.EventSourcing.Samples.Services;

public interface IOrderFulfillmentService
{
    Task<FulfilmentResult> PlaceOrderAsync(
        string customerId,
        string inventoryId,
        int quantity,
        string? shippingAddress,
        CancellationToken cancellationToken = default
    );
}
