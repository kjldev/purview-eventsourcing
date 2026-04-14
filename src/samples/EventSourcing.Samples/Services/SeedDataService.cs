using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Services;

public sealed class SeedDataService(
	IQueryableEventStore store
) : ISeedDataService
{
	static readonly string[] _firstNames =
	[
		"Alice", "Bob", "Carol", "David", "Emma",
		"Frank", "Grace", "Henry", "Iris", "James",
		"Karen", "Liam", "Mia", "Noah", "Olivia",
		"Peter", "Quinn", "Rachel", "Sam", "Tara",
		"Uma", "Victor", "Wendy", "Xavier", "Yara",
		"Zach", "Abby", "Ben", "Clara", "Dan"
	];

	static readonly string[] _lastNames =
	[
		"Smith", "Johnson", "Williams", "Brown", "Jones",
		"Garcia", "Miller", "Davis", "Wilson", "Moore",
		"Taylor", "Anderson", "Thomas", "Jackson", "White",
		"Harris", "Martin", "Thompson", "Young", "Lewis"
	];

	static readonly (string ProductId, string ProductName, int InitialQty, int ReorderQty)[] _products =
	[
		("SKU-001", "Wireless Keyboard", 150, 25),
		("SKU-002", "USB-C Hub 7-Port", 200, 30),
		("SKU-003", "27\" 4K Monitor", 45, 10),
		("SKU-004", "Mechanical Keyboard", 80, 15),
		("SKU-005", "Ergonomic Mouse", 175, 40),
		("SKU-006", "Laptop Stand Aluminium", 120, 20),
		("SKU-007", "Webcam 1080p HD", 95, 20),
		("SKU-008", "Noise Cancelling Headset", 60, 10),
		("SKU-009", "External SSD 1TB", 70, 15),
		("SKU-010", "HDMI 2.1 Cable 2m", 300, 50),
		("SKU-011", "Desk Pad XL", 110, 25),
		("SKU-012", "USB-C Power Bank 20k", 85, 15),
		("SKU-013", "Cable Management Kit", 250, 40),
		("SKU-014", "Monitor Arm Single", 55, 10),
		("SKU-015", "Smart LED Desk Lamp", 90, 20)
	];

	static readonly (string LocationId, string LocationName)[] _locations =
	[
		("LOC-001", "Warehouse North"),
		("LOC-002", "Warehouse South"),
		("LOC-003", "Distribution Centre East"),
		("LOC-004", "Fulfilment Hub West"),
	];

	static readonly decimal[] _unitPrices =
	[
		49.99m, 39.99m, 399.99m, 129.99m, 29.99m,
		59.99m, 79.99m, 149.99m, 89.99m, 12.99m,
		24.99m, 49.99m, 19.99m, 69.99m, 34.99m
	];

	static readonly string[] _addresses =
	[
		"1 King Street, London, EC1A 1BB",
		"42 Queen's Road, Manchester, M1 2AB",
		"15 High Street, Birmingham, B1 1BB",
		"7 Park Lane, Leeds, LS1 1BA",
		"99 Victoria Road, Glasgow, G1 1AA",
		"23 Castle Street, Edinburgh, EH1 1AA",
		"56 Bridge Road, Bristol, BS1 1AA",
		"8 Market Square, Cambridge, CB1 1AA",
		"33 Station Road, Oxford, OX1 1AA",
		"77 Church Lane, Liverpool, L1 1AA"
	];

	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		var customerCount = await store.CountAsync<CustomerAggregate>(null, cancellationToken);
		if (customerCount > 0)
			return;

		var customerIds = await SeedCustomersAsync(cancellationToken);
		var inventoryIds = await SeedInventoryAsync(cancellationToken);
		await SeedOrdersAsync(customerIds, inventoryIds, cancellationToken);
	}

	async Task<string[]> SeedCustomersAsync(CancellationToken cancellationToken)
	{
		var ids = new List<string>();
		var phones = new[] { "+44 7700 900000", "+44 7700 900001", null, "+44 7700 900002", null };

		for (var i = 0; i < _firstNames.Length; i++)
		{
			var first = _firstNames[i];
			var last = _lastNames[i % _lastNames.Length];
			var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.com";
			var phone = phones[i % phones.Length];

			var customer = await store.CreateAsync<CustomerAggregate>(null, cancellationToken);
			customer.RegisterCustomer($"{first} {last}", email);
			if (phone is not null)
				customer.ChangePhoneNumber(phone);

			// Deactivate a handful so the demo shows mixed statuses.
			if (i is 5 or 12 or 21)
				customer.Deactivate();

			var result = await store.SaveAsync(customer, null, cancellationToken);
			ids.Add(result.Aggregate.Id());
		}

		return [.. ids];
	}

	async Task<(string AggregateId, int Index)[]> SeedInventoryAsync(CancellationToken cancellationToken)
	{
		var ids = new List<(string, int)>();

		for (var i = 0; i < _products.Length; i++)
		{
			var (productId, productName, initialQty, _) = _products[i];
			var (locationId, locationName) = _locations[i % _locations.Length];

			var item = await store.CreateAsync<InventoryAggregate>(null, cancellationToken);
			item.Initialize(productId, productName, locationId, locationName, initialQty);

			var result = await store.SaveAsync(item, null, cancellationToken);
			ids.Add((result.Aggregate.Id(), i));
		}

		return [.. ids];
	}

	async Task SeedOrdersAsync(
		string[] customerIds,
		(string AggregateId, int Index)[] inventoryItems,
		CancellationToken cancellationToken
	)
	{
		var rng = new Random(42);

		// 50 orders distributed across customers and products.
		for (var i = 0; i < 50; i++)
		{
			var customerId = customerIds[i % customerIds.Length];
			var (invId, invIdx) = inventoryItems[i % inventoryItems.Length];
			var (productId, productName, _, _) = _products[invIdx];
			var unitPrice = _unitPrices[invIdx];
			var qty = rng.Next(1, 5);
			var address = _addresses[i % _addresses.Length];

			var order = await store.CreateAsync<OrderAggregate>(null, cancellationToken);
			order.CreateOrder(customerId);
			order.AddLineItem(productId, productName, qty, unitPrice);
			order.SetShippingAddress(address);

			// Advance some orders through the lifecycle for variety.
			var stage = i % 5;
			if (stage >= 1) order.ConfirmOrder();
			if (stage >= 2) order.ShipOrder();
			if (stage >= 3) order.CompleteOrder();
			if (stage == 4)
			{
				// Already completed above; create a separate cancelled one.
				var cancelled = await store.CreateAsync<OrderAggregate>(null, cancellationToken);
				cancelled.CreateOrder(customerId);
				cancelled.AddLineItem(productId, productName, 1, unitPrice);
				cancelled.SetShippingAddress(address);
				cancelled.ConfirmOrder();
				cancelled.CancelOrder();
				await store.SaveAsync(cancelled, null, cancellationToken);
			}

			await store.SaveAsync(order, null, cancellationToken);
		}
	}
}
