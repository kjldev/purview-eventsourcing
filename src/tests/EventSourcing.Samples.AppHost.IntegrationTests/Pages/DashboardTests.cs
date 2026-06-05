using Purview.EventSourcing.Samples.AppHost.Infrastructure;

namespace Purview.EventSourcing.Samples.AppHost.Pages;

[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class DashboardTests(AppHostFixture factory)
{
	readonly HttpClient _client = factory.CreateClient();

	[Test]
	public async Task Dashboard_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task Dashboard_ContainsPortalOptions(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(html).Contains("Customer Experience");
		await Assert.That(html).Contains("Back Office");
	}
}
