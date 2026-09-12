using Purview.EventSourcing.Samples.Fixtures;

namespace Purview.EventSourcing.Samples.Web.Pages;

[NotInParallel("SamplesAppHost")]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class AdminSitePageTests(AppHostFixture fixture)
{
	readonly HttpClient _client = fixture.CreateWebClient(followRedirects: true);

	[Test]
	public async Task AdminDashboard_Returns200_AndContainsSearchUI(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/admin", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("Aggregate Search");
	}

	[Test]
	public async Task AdminEventsPage_WithParameters_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync(
			"/admin/events?aggregateType=OrderAggregate&aggregateId=test-aggregate-id",
			cancellationToken
		);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("Event Stream");
	}

	[Test]
	public async Task AdminEventsPage_WithNoEvents_ShowsNoEventsFound(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync(
			"/admin/events?aggregateType=CustomerAggregate&aggregateId=non-existent-aggregate-id",
			cancellationToken
		);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("No events found for the specified filters.");
	}

	[Test]
	public async Task AdminProjectionPage_WithParameters_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync(
			"/admin/projection?aggregateType=OrderAggregate&aggregateId=test-aggregate-id",
			cancellationToken
		);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("Point-in-Time Projection");
	}
}
