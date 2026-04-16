using Purview.EventSourcing.Samples.Web.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class DashboardTests(WebAppFactory factory)
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
