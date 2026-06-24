using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.Samples.Services;
using Purview.EventSourcing.Samples.Web.Services;

namespace Purview.EventSourcing.Samples.Web;

public sealed class AuditPageIntegrationTests
{
	[Test]
	public async Task GetAuditPage_GivenDateRangeOnly_ShowsRecentEvents()
	{
		await using var factory = new AuditWebAppFactory();
		using var client = factory.CreateClient();

		var response = await client.GetAsync("/BackOffice/Audit/Index?aggregateType=order&fromUtc=2026-06-22T00:00");
		var content = await response.Content.ReadAsStringAsync();

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(content).Contains("Recent Events");
		await Assert.That(content).Contains("DateRangeOnlyEvent");
	}

	[Test]
	public async Task GetAuditPage_GivenAggregateId_ShowsAggregateHistoryEvents()
	{
		await using var factory = new AuditWebAppFactory();
		using var client = factory.CreateClient();

		var response = await client.GetAsync("/BackOffice/Audit/Index?aggregateType=order&aggregateId=agg-1");
		var content = await response.Content.ReadAsStringAsync();

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(content).Contains("AggregateIdEvent");
	}

	[Test]
	public async Task GetAuditPage_GivenNoFilters_ShowsRecentEvents()
	{
		await using var factory = new AuditWebAppFactory();
		using var client = factory.CreateClient();

		var response = await client.GetAsync("/BackOffice/Audit/Index");
		var content = await response.Content.ReadAsStringAsync();

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(content).Contains("Recent Events");
		await Assert.That(content).Contains("DateRangeOnlyEvent");
	}

	sealed class AuditWebAppFactory : WebApplicationFactory<Program>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder) =>
			builder.ConfigureTestServices(services =>
			{
				services.RemoveAll<IAggregateAuditService>();
				services.RemoveAll<ISeedDataService>();
				services.AddSingleton<IAggregateAuditService, FakeAggregateAuditService>();
				services.AddSingleton<ISeedDataService, NoOpSeedDataService>();
			});
	}

	sealed class NoOpSeedDataService : ISeedDataService
	{
		public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	sealed class FakeAggregateAuditService : IAggregateAuditService
	{
		public Task<ContinuationResponse<AggregateEventHistoryItem>> GetHistoryAsync(
			string aggregateType,
			string aggregateId,
			AggregateEventHistoryRequest request,
			CancellationToken cancellationToken
		) =>
			Task.FromResult(
				new ContinuationResponse<AggregateEventHistoryItem>
				{
					RequestedCount = request.MaxRecords,
					Results =
					[
						new AggregateEventHistoryItem
						{
							AggregateType = "OrderAggregate",
							AggregateId = aggregateId,
							AggregateVersion = 1,
							EventType = "AggregateIdEvent",
							EventClrType = "AggregateIdEvent",
							Payload = "{}",
							When = DateTimeOffset.UtcNow,
						},
					],
				}
			);

		public Task<IReadOnlyList<AggregateEventHistoryItem>> GetLatestHistoryAsync(
			string aggregateType,
			AggregateEventHistoryRequest request,
			CancellationToken cancellationToken
		) =>
			Task.FromResult<IReadOnlyList<AggregateEventHistoryItem>>([
				new AggregateEventHistoryItem
				{
					AggregateType = "OrderAggregate",
					AggregateId = "range-agg",
					AggregateVersion = 2,
					EventType = "DateRangeOnlyEvent",
					EventClrType = "DateRangeOnlyEvent",
					Payload = "{}",
					When = DateTimeOffset.UtcNow,
				},
			]);
	}
}
