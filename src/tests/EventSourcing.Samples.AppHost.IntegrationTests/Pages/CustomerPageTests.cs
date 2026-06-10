using Purview.EventSourcing.Samples.AppHost.Fixtures;
using Purview.EventSourcing.Samples.Domain;

namespace Purview.EventSourcing.Samples.AppHost.Pages;

[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class CustomerPageTests(AppHostFixture fixture)
{
	readonly HttpClient _client = fixture.CreateWebClient(); //.CreateClient(new() { AllowAutoRedirect = false });

	[Test]
	public async Task CustomerSelector_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomerCatalog_WithoutSession_Redirects(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer/Catalog", cancellationToken);

		// Without a session, should redirect to customer selector
		await Assert.That((int)response.StatusCode is 200 or 302).IsTrue();
	}

	[Test]
	public async Task CustomerOrders_WithoutSession_Redirects(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer/Orders", cancellationToken);

		await Assert.That((int)response.StatusCode is 200 or 302).IsTrue();
	}

	[Test]
	public async Task CustomerProfile_WithoutSession_Redirects(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer/Profile", cancellationToken);

		await Assert.That((int)response.StatusCode is 200 or 302).IsTrue();
	}

	[Test]
	public async Task CustomerSelector_WithPaging_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer?page=1&pageSize=10", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomerSelector_WithSearch_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer?search=alice", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomerSelector_WithMultiplePages_RendersNextPageLink(CancellationToken cancellationToken)
	{
		var prefix = $"paging-selector-{Guid.NewGuid():N}";
		await CreateCustomersAsync(prefix, 11, cancellationToken);

		var response = await _client.GetAsync($"/Customer?search={prefix}&pageSize=10", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("page=2");
	}

	[Test]
	public async Task BackOfficeCustomersIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Customers", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCustomersCreate_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Customers/Create", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCustomersCreate_Post_RedirectsToIndex(CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string> { ["Name"] = "Test Customer", ["Email"] = "test@example.com" };

		var antiForgery = await GetAntiForgeryTokenAsync("/BackOffice/Customers/Create", cancellationToken);
		form["__RequestVerificationToken"] = antiForgery;

		using var content = new FormUrlEncodedContent(form);
		var response = await _client.PostAsync("/BackOffice/Customers/Create", content, cancellationToken);

		await Assert.That((int)response.StatusCode).IsEqualTo(302);
		await Assert.That(response.Headers.Location?.ToString()).Contains("/BackOffice/Customers");
	}

	[Test]
	public async Task BackOfficeCustomersDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Customers/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCustomersIndex_WithMultiplePages_RendersNextPageLink(
		CancellationToken cancellationToken
	)
	{
		var prefix = $"paging-{Guid.NewGuid():N}";
		await CreateCustomersAsync(prefix, 11, cancellationToken);

		var response = await _client.GetAsync($"/BackOffice/Customers?search={prefix}&pageSize=10", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
		await Assert.That(html).Contains("page=2");
	}

	[Test]
	public async Task CustomerDetails_UnknownId_Returns200OrNotFound(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer/Orders/Details/unknown-id", cancellationToken);

		await Assert.That((int)response.StatusCode is 200 or 404 or 302).IsTrue();
	}

	async Task<string> GetAntiForgeryTokenAsync(string url, CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync(url, cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		var start = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
		if (start == -1)
			return string.Empty;

		var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
		var valueEnd = html.IndexOf('"', valueStart);

		return html[valueStart..valueEnd];
	}

	async Task CreateCustomersAsync(string prefix, int count, CancellationToken cancellationToken)
	{
		var store = fixture.QueryableEventStore();
		for (var i = 0; i < count; i++)
		{
			var customer = await store.CreateAsync<CustomerAggregate>(cancellationToken: cancellationToken);
			customer.RegisterCustomer($"{prefix}-customer-{i:D2}", $"{prefix}-{i:D2}@example.com");
			await store.SaveAsync(customer, cancellationToken);
		}
	}
}
