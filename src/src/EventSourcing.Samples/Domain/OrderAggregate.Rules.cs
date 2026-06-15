using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

partial class OrderAggregate
{
	partial void OnShippingAddressChanging(ref string? shippingAddress)
	{
		shippingAddress = shippingAddress.OrNull();

		if (Status.Value is not OrderStatusCode.Draft)
			throw new InvalidOperationException("Shipping address can only be set while order is in draft status.");
	}

	partial void OnStatusChanging(ref OrderStatus status)
	{
		switch (status.Value)
		{
			case OrderStatusCode.Confirmed when Status != OrderStatus.Draft:
			case OrderStatusCode.Shipped when Status != OrderStatus.Confirmed:
			case OrderStatusCode.Completed when Status != OrderStatus.Shipped:
			case OrderStatusCode.Cancelled when Status == OrderStatus.Shipped:
				throw new InvalidOperationException($"Invalid status transition from {Status} to {status}.");
		}
	}

	partial void OnRaisingLineItemAddedEvent(ref List<OrderLineItem> lineItems, ref decimal totalAmount)
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only add items to draft orders.");
	}

	partial void OnRaisingLineItemRemovedEvent(ref List<OrderLineItem> lineItems, ref decimal totalAmount)
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Can only remove items from draft orders.");
	}

	partial void OnCustomerIdChanging(ref string customerId) => ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
}
