namespace Purview.EventSourcing.Samples.Services;

public interface ICartCheckoutService
{
    Task<CartCheckoutResult> CheckoutAsync(
        string customerId,
        IReadOnlyList<CartItem> items,
        string? shippingAddress,
        CancellationToken cancellationToken = default
    );
}
