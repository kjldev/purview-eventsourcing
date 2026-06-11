using System.Text.Json;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.ValueObjects;
using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.Serialization;

public sealed class SampleAggregateJsonSerializationTests
{
	[Test]
	public async Task SerializeAndDeserialize_GivenOrderAggregate_RestoresGeneratedAggregateState()
	{
		var aggregate = new OrderAggregate();
		aggregate.Details.Id = "order-1";
		aggregate.CreateOrder("customer-1");
		aggregate.AddLineItem("product-1", "Widget", 2, 12.5m);
		aggregate.SetShippingAddress("123 Main Street");
		aggregate.UpdateNotes("Fragile");
		aggregate.ConfirmOrder();

		var json = EventStoreSerializationHelpers.Serialize(aggregate);
		var roundTripped = EventStoreSerializationHelpers.Deserialize<OrderAggregate>(json);

		await Assert.That(roundTripped).IsNotNull();
		await Assert.That(roundTripped!.CustomerId).IsEqualTo("customer-1");
		await Assert.That(roundTripped.LineItems).Count().IsEqualTo(1);
		await Assert.That(roundTripped.LineItems[0].ProductId).IsEqualTo("product-1");
		await Assert.That(roundTripped.TotalAmount).IsEqualTo(25m);
		await Assert.That(roundTripped.ShippingAddress).IsEqualTo("123 Main Street");
		await Assert.That(roundTripped.Notes).IsEqualTo("Fragile");
		await Assert.That(roundTripped.Status).IsEqualTo(OrderStatus.Confirmed);
		await Assert.That(roundTripped.Details.Id).IsEqualTo("order-1");
	}

	[Test]
	public async Task SerializeAndDeserialize_GivenCustomerAggregate_RestoresGeneratedAggregateState()
	{
		var aggregate = new CustomerAggregate();
		aggregate.Details.Id = "customer-9";
		aggregate.RegisterCustomer("Alice", "alice@example.com");
		aggregate.ChangePhoneNumber("555-0100");
		aggregate.Deactivate();

		await Assert.That(aggregate.Name).IsEqualTo("Alice");
		await Assert.That(aggregate.Email).IsEqualTo("alice@example.com");
		await Assert.That(aggregate.PhoneNumber).IsEqualTo("555-0100");
		await Assert.That(aggregate.IsActive).IsFalse();
		await Assert.That(aggregate.Details.Id).IsEqualTo("customer-9");

		var json = EventStoreSerializationHelpers.Serialize(aggregate);
		using var jsonDocument = JsonDocument.Parse(json);
		await Assert.That(jsonDocument.RootElement.GetProperty("Name").ValueKind).IsEqualTo(JsonValueKind.String);
		await Assert.That(jsonDocument.RootElement.GetProperty("Email").ValueKind).IsEqualTo(JsonValueKind.String);

		var roundTripped = EventStoreSerializationHelpers.Deserialize<CustomerAggregate>(json);

		await Assert.That(roundTripped).IsNotNull();
		await Assert.That(roundTripped!.Name).IsEqualTo("Alice");
		await Assert.That(roundTripped.Email).IsEqualTo("alice@example.com");
		await Assert.That(roundTripped.PhoneNumber).IsEqualTo("555-0100");
		await Assert.That(roundTripped.IsActive).IsFalse();
		await Assert.That(roundTripped.Details.Id).IsEqualTo("customer-9");
	}
}
