using Purview.EventSourcing.Samples.Web.IntegrationTests.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.IntegrationTests.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class CustomerPageTests(WebAppFactory factory)
{
	readonly HttpClient _client = factory.CreateClient(
		new() { AllowAutoRedirect = false }
	);

	[Test]
	public async Task CustomersIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersCreate_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers/Create", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersCreate_Post_RedirectsToIndex(CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string>
		{
			["Name"] = "Test Customer",
			["Email"] = "test@example.com"
		};

		var antiForgery = await GetAntiForgeryTokenAsync("/Customers/Create", cancellationToken);
		form["__RequestVerificationToken"] = antiForgery;

		using var content = new FormUrlEncodedContent(form);
		var response = await _client.PostAsync("/Customers/Create", content, cancellationToken);

		await Assert.That((int)response.StatusCode).IsEqualTo(302);
		await Assert.That(response.Headers.Location?.ToString()).Contains("/Customers");
	}

	[Test]
	public async Task CustomersDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersIndex_WithPaging_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers?page=1&pageSize=10", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersIndex_WithSorting_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers?sortBy=email&sortDir=asc", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersIndex_WithSearch_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers?search=alice", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomersIndex_Page2_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers?page=2&pageSize=10", cancellationToken);

		// Page 2 is valid as long as there's seed data (30+ customers)
		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomerDetails_UnknownId_Returns200OrNotFound(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customers/unknown-id", cancellationToken);

		// Returns 200 with "not found" message OR a proper 404
		await Assert.That((int)response.StatusCode is 200 or 404 or 302).IsTrue();
	}

	async Task<string> GetAntiForgeryTokenAsync(string url, CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync(url, cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		var start = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
		if (start == -1) return string.Empty;

		var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
		var valueEnd = html.IndexOf('"', valueStart);

		return html[valueStart..valueEnd];
	}
}
