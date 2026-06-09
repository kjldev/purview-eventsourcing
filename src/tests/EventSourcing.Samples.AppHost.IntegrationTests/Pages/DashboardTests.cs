using Purview.EventSourcing.Samples.AppHost.Infrastructure;

namespace Purview.EventSourcing.Samples.AppHost.Pages;

[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
[Skip("WIP")]
public sealed class DashboardTests(AppHostFixture factory)
{
	[Test]
	public async Task Dashboard_Returns200(CancellationToken cancellationToken)
	{
		var client = factory.CreateWebClient();
		var response = await client.GetAsync("/", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task Dashboard_ContainsPortalOptions(CancellationToken cancellationToken)
	{
		var client = factory.CreateWebClient();
		var response = await client.GetAsync("/", cancellationToken);

		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(html).Contains("Customer Experience");
		await Assert.That(html).Contains("Back Office");
	}
}
