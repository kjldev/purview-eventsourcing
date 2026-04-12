using Purview.EventSourcing.Samples.Web.IntegrationTests.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.IntegrationTests.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class InventoryPageTests(WebAppFactory factory)
{
	readonly HttpClient _client = factory.CreateClient(
		new() { AllowAutoRedirect = false }
	);

	[Test]
	public async Task BackOfficeCatalogIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogCreate_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog/Create", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogCreate_Post_RedirectsToIndex(CancellationToken cancellationToken)
	{
		var antiForgery = await GetAntiForgeryTokenAsync("/BackOffice/Catalog/Create", cancellationToken);
		var form = new Dictionary<string, string>
		{
			["ProductId"] = "SKU-TEST-001",
			["ProductName"] = "Test Widget",
			["LocationId"] = "WH-TEST",
			["LocationName"] = "Test Warehouse",
			["InitialQuantity"] = "50",
			["__RequestVerificationToken"] = antiForgery
		};

		using var content = new FormUrlEncodedContent(form);
		var response = await _client.PostAsync("/BackOffice/Catalog/Create", content, cancellationToken);

		await Assert.That((int)response.StatusCode).IsEqualTo(302);
		await Assert.That(response.Headers.Location?.ToString()).Contains("/BackOffice/Catalog");
	}

	[Test]
	public async Task BackOfficeCatalogDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeStockIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Stock", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeStockCreate_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Stock/Create", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeStockDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Stock/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogIndex_WithPaging_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog?page=1&pageSize=10", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogIndex_WithSorting_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog?sortBy=productid&sortDir=desc", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogIndex_WithSearch_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog?search=widget", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
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
