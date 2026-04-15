using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.Samples.Web.IntegrationTests.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.IntegrationTests.Services;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class CartCheckoutServiceTests(WebAppFactory factory)
{
	[Test]
	public async Task CheckoutAsync_GivenAvailableStock_CreatesOrderAndReservesInventoryAtomically(
		CancellationToken cancellationToken
	)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		var serviceProvider = scope.ServiceProvider;

		var checkoutService = serviceProvider.GetRequiredService<ICartCheckoutService>();
		var customerStore = serviceProvider.GetRequiredService<IQueryableEventStore>();
		var inventoryStore = serviceProvider.GetRequiredService<IQueryableEventStore>();
		var orderStore = serviceProvider.GetRequiredService<IQueryableEventStore>();

		var customer = await CreateCustomerAsync(customerStore, cancellationToken);
		var inventory = await CreateInventoryAsync(inventoryStore, quantityOnHand: 10, cancellationToken);

		var result = await checkoutService.CheckoutAsync(
			customer.Id(),
			[new CartItem(inventory.ProductId, inventory.ProductName, inventory.Id(), 3, 19.99m)],
			"1 Event Sourcing Way",
			cancellationToken
		);

		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(result.Order).IsNotNull();

		var savedOrder = await orderStore.GetAsync<OrderAggregate>(result.Order!.Id(), null, cancellationToken);
		var savedInventory = await inventoryStore.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken);
		var orderCount = await orderStore.CountAsync<OrderAggregate>(
			order => order.CustomerId == customer.Id(),
			cancellationToken
		);

		await Assert.That(orderCount).IsEqualTo(1L);
		await Assert.That(savedOrder).IsNotNull();
		await Assert.That(savedOrder!.Status).IsEqualTo(OrderStatus.Confirmed);
		await Assert.That(savedOrder.TotalAmount).IsEqualTo(result.Order.TotalAmount);
		await Assert.That(savedInventory).IsNotNull();
		await Assert.That(savedInventory!.ReservedQuantity).IsEqualTo(3);
		await Assert.That(savedInventory.AvailableQuantity).IsEqualTo(7);
	}

	[Test]
	public async Task CheckoutAsync_WhenInventoryChangesBeforeCommit_RollsBackOrderAndPreservesAtomicity(
		CancellationToken cancellationToken
	)
	{
		var inventoryId = string.Empty;

		using var customizedFactory = factory.WithWebHostBuilder(builder =>
		{
			builder.ConfigureServices(services =>
			{
				var descriptor = services.Last(service => service.ServiceType == typeof(IEventStoreTransactionFactory));
				services.Remove(descriptor);
				services.AddSingleton<IEventStoreTransactionFactory>(serviceProvider => new HookedTransactionFactory(
					CreateService<IEventStoreTransactionFactory>(serviceProvider, descriptor),
					async hookCancellationToken =>
					{
						await using var hookScope = serviceProvider.CreateAsyncScope();
						var inventoryStore = hookScope.ServiceProvider.GetRequiredService<IQueryableEventStore>();
						var inventory = await inventoryStore.GetAsync<InventoryAggregate>(
							inventoryId,
							null,
							hookCancellationToken
						);

						inventory!.ReserveStock(3, "concurrent-order");
						await inventoryStore.SaveAsync<InventoryAggregate>(inventory, null, hookCancellationToken);
					}
				));
			});
		});

		await using var scope = customizedFactory.Services.CreateAsyncScope();
		var serviceProvider = scope.ServiceProvider;

		var checkoutService = serviceProvider.GetRequiredService<ICartCheckoutService>();
		var customerStore = serviceProvider.GetRequiredService<IQueryableEventStore>();
		var inventoryStore = serviceProvider.GetRequiredService<IQueryableEventStore>();
		var orderStore = serviceProvider.GetRequiredService<IQueryableEventStore>();

		var customer = await CreateCustomerAsync(customerStore, cancellationToken);
		var inventory = await CreateInventoryAsync(inventoryStore, quantityOnHand: 10, cancellationToken);
		inventoryId = inventory.Id();

		var result = await checkoutService.CheckoutAsync(
			customer.Id(),
			[new CartItem(inventory.ProductId, inventory.ProductName, inventory.Id(), 8, 19.99m)],
			"2 Event Sourcing Way",
			cancellationToken
		);

		var orderCount = await orderStore.CountAsync<OrderAggregate>(
			order => order.CustomerId == customer.Id(),
			cancellationToken
		);
		var savedInventory = await inventoryStore.GetAsync<InventoryAggregate>(inventory.Id(), null, cancellationToken);

		await Assert.That(result.Succeeded).IsFalse();
		await Assert.That(result.ErrorMessage).Contains("Nothing was saved");
		await Assert.That(orderCount).IsEqualTo(0L);
		await Assert.That(savedInventory).IsNotNull();
		await Assert.That(savedInventory!.ReservedQuantity).IsEqualTo(3);
		await Assert.That(savedInventory.AvailableQuantity).IsEqualTo(7);
	}

	static T CreateService<T>(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
		where T : class
	{
		if (descriptor.ImplementationInstance is T instance)
			return instance;

		if (descriptor.ImplementationFactory is not null)
			return (T)descriptor.ImplementationFactory(serviceProvider);

		return (T)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
	}

	static async Task<CustomerAggregate> CreateCustomerAsync(
		IQueryableEventStore customerStore,
		CancellationToken cancellationToken
	)
	{
		var customer = await customerStore.CreateAsync<CustomerAggregate>(null, cancellationToken);
		customer.RegisterCustomer($"Cart Test {Guid.NewGuid():N}", $"{Guid.NewGuid():N}@example.com");

		var result = await customerStore.SaveAsync<CustomerAggregate>(customer, null, cancellationToken);
		return result.Aggregate;
	}

	static async Task<InventoryAggregate> CreateInventoryAsync(
		IQueryableEventStore inventoryStore,
		int quantityOnHand,
		CancellationToken cancellationToken
	)
	{
		var inventory = await inventoryStore.CreateAsync<InventoryAggregate>(null, cancellationToken);
		inventory.Initialize(
			$"sku-{Guid.NewGuid():N}",
			"Transactional Widget",
			$"loc-{Guid.NewGuid():N}",
			"Main Warehouse",
			quantityOnHand
		);

		var result = await inventoryStore.SaveAsync<InventoryAggregate>(inventory, null, cancellationToken);
		return result.Aggregate;
	}

	sealed class HookedTransactionFactory(
		IEventStoreTransactionFactory innerFactory,
		Func<CancellationToken, Task> beforeCommit
	) : IEventStoreTransactionFactory
	{
		public IEventStoreTransaction Create(string? correlationId = null) =>
			new HookedTransaction(innerFactory.Create(correlationId), beforeCommit);
	}

	sealed class HookedTransaction(IEventStoreTransaction innerTransaction, Func<CancellationToken, Task> beforeCommit)
		: IEventStoreTransaction
	{
		int _beforeCommitInvoked;

		public string CorrelationId => innerTransaction.CorrelationId;

		public void Enlist<T>(T aggregate, IEventStore eventStore, EventStoreOperationContext? operationContext = null)
			where T : class, IAggregate, new() => innerTransaction.Enlist(aggregate, eventStore, operationContext);

		public void Enlist<T>(
			T aggregate,
			IEventStoreCore<T> eventStore,
			EventStoreOperationContext? operationContext = null
		)
			where T : class, IAggregate, new() => innerTransaction.Enlist(aggregate, eventStore, operationContext);

		public async Task<TransactionResult> CommitAsync(CancellationToken cancellationToken = default)
		{
			if (Interlocked.Exchange(ref _beforeCommitInvoked, 1) == 0)
				await beforeCommit(cancellationToken);

			return await innerTransaction.CommitAsync(cancellationToken);
		}

		public ValueTask DisposeAsync() => innerTransaction.DisposeAsync();
	}
}
