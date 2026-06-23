using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.IntegrationTests.Services;

[ClassDataSource<SqlServerEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class AggregateAuditServiceIntegrationTests(SqlServerEventStoreFixture fixture)
{
	[Test]
	public async Task GetLatestHistoryAsync_GivenDateRangeOnly_ReturnsDatabaseEvents(
		CancellationToken cancellationToken
	)
	{
		var orderStore = fixture.CreateEventStore<OrderAggregate>();
		var eventStore = CreateEventStoreFacade(orderStore);
		var service = new AggregateAuditService(eventStore);

		var order = await orderStore.CreateAsync(cancellationToken: cancellationToken);
		order.CreateOrder("customer-1").AddLineItem("sku-1", "Widget", 1, 10m).ConfirmOrder();
		await orderStore.SaveAsync(order, null, cancellationToken);

		var response = await service.GetLatestHistoryAsync(
			"order",
			new AggregateEventHistoryRequest
			{
				FromUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
				ToUtc = DateTimeOffset.UtcNow.AddMinutes(5),
				MaxRecords = 20,
			},
			cancellationToken
		);

		await Assert.That(response.Count).IsGreaterThan(0);
		await Assert.That(response.Any(m => m.AggregateId == order.Id())).IsTrue();
	}

	static IEventStore CreateEventStoreFacade(IEventStoreCore<OrderAggregate> orderStore)
	{
		var services = new ServiceCollection();
		services.AddSingleton(orderStore);
		services.AddSingleton<IEventStoreCore<OrderAggregate>>(orderStore);
		services.AddSingleton<IEventStore, EventStoreFacade>();
		return services.BuildServiceProvider().GetRequiredService<IEventStore>();
	}
}
