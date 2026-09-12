using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.Web.Services;

[ClassDataSource<SqlServerEventStoreFixture>(Shared = SharedType.PerTestSession)]
public sealed class AggregateAuditServiceIntegrationTests(SqlServerEventStoreFixture fixture)
{
	[Test]
	public async Task GetLatestHistoryAsync_GivenDateRangeOnly_ReturnsDatabaseEvents(
		CancellationToken cancellationToken
	)
	{
		var orderStore = fixture.CreateEventStore<OrderAggregate>();
		var eventStore = CreateEventStoreFacade(orderStore);
		AggregateAuditService service = new(eventStore);

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
		ServiceCollection services = new();
		services.AddSingleton(orderStore);
		services.AddSingleton(orderStore);
		services.AddSingleton<IEventStore, EventStoreFacade>();
		return services.BuildServiceProvider().GetRequiredService<IEventStore>();
	}
}
