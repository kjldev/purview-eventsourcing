using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.Domain;

public class OrderAggregateTests
{
    static OrderAggregate CreateOrder(string? id = null, bool withItems = false)
    {
        var order = new OrderAggregate();
        if (id is not null)
            order.Details.Id = id;
        order.CreateOrder("customer-1");

        if (withItems)
        {
            order.AddLineItem("prod-1", "Widget A", 2, 10.00m);
            order.AddLineItem("prod-2", "Widget B", 1, 25.00m);
        }

        return order;
    }

    #region CreateOrder Tests

    [Test]
    public async Task CreateOrder_GivenValidCustomerId_SetsProperties()
    {
        // Arrange & Act
        var order = new OrderAggregate();
        order.Details.Id = "order-1";
        order.CreateOrder("customer-1");

        // Assert
        await Assert.That(order.CustomerId).IsEqualTo("customer-1");
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Draft);
        await Assert.That(order.TotalAmount).IsEqualTo(0m);
        await Assert.That(order.LineItems).IsEmpty();
    }

    [Test]
    public void CreateOrder_GivenNullCustomerId_ThrowsArgumentException()
    {
        var order = new OrderAggregate();
        Assert.Throws<ArgumentException>(() => order.CreateOrder(null!));
    }

    #endregion

    #region AddLineItem Tests

    [Test]
    public async Task AddLineItem_GivenValidItem_AddsToLineItems()
    {
        // Arrange
        var order = CreateOrder("order-1");

        // Act
        order.AddLineItem("prod-1", "Widget A", 2, 29.99m);

        // Assert
        await Assert.That(order.LineItems).Count().IsEqualTo(1);
        await Assert.That(order.LineItems[0].ProductId).IsEqualTo("prod-1");
        await Assert.That(order.LineItems[0].Quantity).IsEqualTo(2);
        await Assert.That(order.TotalAmount).IsEqualTo(59.98m);
    }

    [Test]
    public async Task AddLineItem_GivenDuplicateProduct_IncreasesQuantity()
    {
        // Arrange
        var order = CreateOrder("order-1");
        order.AddLineItem("prod-1", "Widget A", 2, 10.00m);

        // Act
        order.AddLineItem("prod-1", "Widget A", 3, 10.00m);

        // Assert — same product, quantity merged
        await Assert.That(order.LineItems).Count().IsEqualTo(1);
        await Assert.That(order.LineItems[0].Quantity).IsEqualTo(5);
        await Assert.That(order.TotalAmount).IsEqualTo(50.00m);
    }

    [Test]
    public void AddLineItem_GivenZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        var order = CreateOrder("order-1");
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddLineItem("p1", "Name", 0, 10m));
    }

    [Test]
    public void AddLineItem_GivenConfirmedOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = CreateOrder("order-1", withItems: true);
        order.ConfirmOrder();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => order.AddLineItem("p3", "Name", 1, 10m));
    }

    #endregion

    #region RemoveLineItem Tests

    [Test]
    public async Task RemoveLineItem_GivenExistingProduct_RemovesFromList()
    {
        // Arrange
        var order = CreateOrder("order-1", withItems: true);

        // Act
        order.RemoveLineItem("prod-1");

        // Assert
        await Assert.That(order.LineItems).Count().IsEqualTo(1);
        await Assert.That(order.TotalAmount).IsEqualTo(25.00m);
    }

    [Test]
    public void RemoveLineItem_GivenNonExistentProduct_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1", withItems: true);
        Assert.Throws<InvalidOperationException>(() => order.RemoveLineItem("nonexistent"));
    }

    #endregion

    #region Order Lifecycle Tests

    [Test]
    public async Task FullLifecycle_DraftToCompleted_TransitionsCorrectly()
    {
        // Arrange
        var order = CreateOrder("order-1", withItems: true);
        order.SetShippingAddress("123 Main St");

        // Act & Assert each transition
        order.ConfirmOrder();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Confirmed);

        order.ShipOrder();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Shipped);
        await Assert.That(order.ShippedAt).IsNotNull();

        order.CompleteOrder();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Completed);
        await Assert.That(order.CompletedAt).IsNotNull();
    }

    [Test]
    public void ConfirmOrder_GivenNoItems_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1");
        Assert.Throws<InvalidOperationException>(() => order.ConfirmOrder());
    }

    [Test]
    public void ShipOrder_GivenNoShippingAddress_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1", withItems: true);
        order.ConfirmOrder();
        Assert.Throws<InvalidOperationException>(() => order.ShipOrder());
    }

    [Test]
    public void ShipOrder_GivenDraftOrder_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1", withItems: true);
        Assert.Throws<InvalidOperationException>(() => order.ShipOrder());
    }

    [Test]
    public void CompleteOrder_GivenConfirmedOrder_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1", withItems: true);
        order.ConfirmOrder();
        Assert.Throws<InvalidOperationException>(() => order.CompleteOrder());
    }

    #endregion

    #region CancelOrder Tests

    [Test]
    public async Task CancelOrder_GivenDraftOrder_SetsCancelledStatus()
    {
        var order = CreateOrder("order-1", withItems: true);
        order.CancelOrder();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
    }

    [Test]
    public async Task CancelOrder_GivenConfirmedOrder_SetsCancelledStatus()
    {
        var order = CreateOrder("order-1", withItems: true);
        order.ConfirmOrder();
        order.CancelOrder();
        await Assert.That(order.Status).IsEqualTo(OrderStatus.Cancelled);
    }

    [Test]
    public void CancelOrder_GivenCompletedOrder_ThrowsInvalidOperationException()
    {
        var order = CreateOrder("order-1", withItems: true);
        order.SetShippingAddress("123 St");
        order.ConfirmOrder();
        order.ShipOrder();
        order.CompleteOrder();
        Assert.Throws<InvalidOperationException>(() => order.CancelOrder());
    }

    #endregion

    #region UpdateDetails Tests

    [Test]
    public async Task UpdateDetails_GivenShippingAddressAndNotes_RaisesTwoEvents()
    {
        var order = CreateOrder("order-1");
        var countBefore = order.GetUnsavedEvents().Count();

        order.UpdateDetails(shippingAddress: "456 New Street", notes: "Handle with care");

        await Assert.That(order.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 2);
        await Assert.That(order.ShippingAddress).IsEqualTo("456 New Street");
        await Assert.That(order.Notes).IsEqualTo("Handle with care");
    }

    [Test]
    public async Task UpdateDetails_GivenOnlyShippingAddress_RaisesOneEvent()
    {
        var order = CreateOrder("order-1");
        order.UpdateNotes("Old note");
        var countBefore = order.GetUnsavedEvents().Count();

        order.UpdateDetails(shippingAddress: "789 Commerce Blvd");

        await Assert.That(order.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 1);
        await Assert.That(order.ShippingAddress).IsEqualTo("789 Commerce Blvd");
        await Assert.That(order.Notes).IsEqualTo("Old note");
    }

    [Test]
    public async Task UpdateDetails_GivenSameValues_RaisesNoEvents()
    {
        var order = CreateOrder("order-1");
        order.UpdateDetails(shippingAddress: "123 Main St", notes: "Urgent");
        var countBefore = order.GetUnsavedEvents().Count();

        order.UpdateDetails(shippingAddress: "123 Main St", notes: "Urgent");

        await Assert.That(order.GetUnsavedEvents().Count()).IsEqualTo(countBefore);
    }

    [Test]
    public void UpdateDetails_GivenWhitespaceAddress_ThrowsArgumentException()
    {
        var order = CreateOrder("order-1");

        Assert.Throws<ArgumentException>(() => order.UpdateDetails(shippingAddress: "  "));
    }

    #endregion

    #region Event Tracking Tests

    [Test]
    public async Task FullWorkflow_TracksAllEvents()
    {
        // Arrange & Act
        var order = CreateOrder("order-1");
        order.AddLineItem("p1", "Widget", 1, 10m);
        order.SetShippingAddress("123 St");
        order.UpdateNotes("Urgent");
        order.ConfirmOrder();
        order.ShipOrder();
        order.CompleteOrder();

        // Assert — CreateOrder + AddLineItem + SetShippingAddress + UpdateNotes + Confirm + Ship + Complete = 7
        await Assert.That(order.GetUnsavedEvents().Count()).IsEqualTo(7);
        await Assert.That(order.Details.CurrentVersion).IsEqualTo(7);
    }

    #endregion
}
