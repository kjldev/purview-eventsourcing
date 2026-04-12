using Purview.EventSourcing.Samples.Web.IntegrationTests.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.IntegrationTests.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class OrderPageTests(WebAppFactory factory)
{
	readonly HttpClient _client = factory.CreateClient(
		new() { AllowAutoRedirect = false }
	);

	[Test]
	public async Task BackOfficeIndex_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task CustomerOrders_WithoutSession_RedirectsOrReturns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/Customer/Orders", cancellationToken);

		await Assert.That((int)response.StatusCode is 200 or 302).IsTrue();
	}

	[Test]
	public async Task BackOfficeStockTransfer_Get_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Stock/Transfer", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeStockTransfer_Post_WithValidData_Redirects(CancellationToken cancellationToken)
	{
		var antiForgery = await GetAntiForgeryTokenAsync("/BackOffice/Stock/Transfer", cancellationToken);

		var pageResponse = await _client.GetAsync("/BackOffice/Stock/Transfer", cancellationToken);
		var html = await pageResponse.Content.ReadAsStringAsync(cancellationToken);

		var sourceId = ExtractFirstSelectValue(html, "SourceId");
		var destId = ExtractSecondSelectValue(html, "DestinationId", sourceId);

		if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(destId) || sourceId == destId)
		{
			await Assert.That(pageResponse.IsSuccessStatusCode).IsTrue();
			return;
		}

		var form = new Dictionary<string, string>
		{
			["SourceId"] = sourceId,
			["DestinationId"] = destId,
			["Quantity"] = "1",
			["Reason"] = "Integration test transfer",
			["__RequestVerificationToken"] = antiForgery
		};

		using var content = new FormUrlEncodedContent(form);
		var response = await _client.PostAsync("/BackOffice/Stock/Transfer", content, cancellationToken);

		await Assert.That((int)response.StatusCode).IsEqualTo(302);
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
		var response = await _client.GetAsync("/BackOffice/Catalog?sortBy=name&sortDir=asc", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	[Test]
	public async Task BackOfficeCatalogDeleted_Returns200(CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync("/BackOffice/Catalog/Deleted", cancellationToken);

		await Assert.That(response.IsSuccessStatusCode).IsTrue();
	}

	static string ExtractFirstSelectValue(string html, string selectName)
	{
		var marker = $"name=\"{selectName}\"";
		var selectStart = html.IndexOf(marker, StringComparison.Ordinal);
		if (selectStart == -1) return string.Empty;

		var searchFrom = selectStart;
		while (true)
		{
			var optionStart = html.IndexOf("<option value=\"", searchFrom, StringComparison.Ordinal);
			if (optionStart == -1) return string.Empty;

			var valueStart = optionStart + 15;
			var valueEnd = html.IndexOf('"', valueStart);
			if (valueEnd == -1) return string.Empty;

			var value = html[valueStart..valueEnd];
			if (!string.IsNullOrEmpty(value))
				return value;

			searchFrom = valueEnd + 1;
		}
	}

	static string ExtractSecondSelectValue(string html, string selectName, string excludeValue)
	{
		var marker = $"name=\"{selectName}\"";
		var selectStart = html.IndexOf(marker, StringComparison.Ordinal);
		if (selectStart == -1) return string.Empty;

		var searchFrom = selectStart;
		while (true)
		{
			var optionStart = html.IndexOf("<option value=\"", searchFrom, StringComparison.Ordinal);
			if (optionStart == -1) return string.Empty;

			var valueStart = optionStart + 15;
			var valueEnd = html.IndexOf('"', valueStart);
			if (valueEnd == -1) return string.Empty;

			var value = html[valueStart..valueEnd];
			if (!string.IsNullOrEmpty(value) && value != excludeValue)
				return value;

			searchFrom = valueEnd + 1;
		}
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
