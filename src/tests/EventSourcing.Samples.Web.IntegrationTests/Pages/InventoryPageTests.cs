using Purview.EventSourcing.Samples.Web.IntegrationTests.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.IntegrationTests.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class InventoryPageTests(WebAppFactory factory)
{
	readonly HttpClient _client = factory.CreateClient(
		new() { AllowAutoRedirect = false }
	);

	[Test]
	public async Task InventoryIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task InventoryCreate_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory/Create", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task InventoryCreate_Post_RedirectsToIndex(CancellationToken cancellationToken)
	{
		var antiForgery = await GetAntiForgeryTokenAsync("/Inventory/Create", cancellationToken);
		var form = new Dictionary<string, string>
		{
			["ProductId"] = "SKU-TEST-001",
			["ProductName"] = "Test Widget",
			["InitialQuantity"] = "50",
			["__RequestVerificationToken"] = antiForgery
		};

		using var content = new FormUrlEncodedContent(form);
		var response = await _client.PostAsync("/Inventory/Create", content, cancellationToken);

		await Assert.That((int)response.StatusCode).IsEqualTo(302);
		await Assert.That(response.Headers.Location?.ToString()).Contains("/Inventory");
	}

	[Test]
	public async Task InventoryDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task InventoryIndex_WithPaging_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory?page=1&pageSize=10", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task InventoryIndex_WithSorting_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory?sortBy=onhand&sortDir=desc", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task InventoryIndex_WithSearch_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Inventory?search=widget", cancellationToken);

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
