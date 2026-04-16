using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Web.Infrastructure;

namespace Purview.EventSourcing.Samples.Web.Pages;

[ClassDataSource<WebAppFactory>(Shared = SharedType.PerTestSession)]
public sealed class OrderPageTests(WebAppFactory factory)
{
	readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });

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
		var (sourceInventoryId, destinationLocationId) = await CreateTransferScenarioAsync(cancellationToken);
		var antiForgery = await GetAntiForgeryTokenAsync("/BackOffice/Stock/Transfer", cancellationToken);

		var form = new Dictionary<string, string>
		{
			["SourceInventoryId"] = sourceInventoryId,
			["DestinationLocationId"] = destinationLocationId,
			["Quantity"] = "1",
			["Reason"] = "Integration test transfer",
			["__RequestVerificationToken"] = antiForgery,
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

	async Task<(string SourceInventoryId, string DestinationLocationId)> CreateTransferScenarioAsync(
		CancellationToken cancellationToken
	)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IQueryableEventStore>();

		var sourceLocationId = $"LOC-TEST-SRC-{Guid.NewGuid():N}";
		var sourceLocation = await store.CreateAsync<LocationAggregate>(sourceLocationId, cancellationToken);
		sourceLocation.Initialize(sourceLocationId, $"Transfer Source {Guid.NewGuid():N}");
		await store.SaveAsync(sourceLocation, null, cancellationToken);

		var destinationLocationId = $"LOC-TEST-DST-{Guid.NewGuid():N}";
		var destinationLocation = await store.CreateAsync<LocationAggregate>(destinationLocationId, cancellationToken);
		destinationLocation.Initialize(destinationLocationId, $"Transfer Destination {Guid.NewGuid():N}");
		await store.SaveAsync(destinationLocation, null, cancellationToken);

		var inventory = await store.CreateAsync<InventoryAggregate>(null, cancellationToken);
		inventory.Initialize(
			$"SKU-TRANSFER-{Guid.NewGuid():N}",
			"Transfer Test Widget",
			sourceLocationId,
			sourceLocation.LocationName,
			initialQuantity: 10
		);

		var saveResult = await store.SaveAsync(inventory, null, cancellationToken);
		return (saveResult.Aggregate.Id(), destinationLocationId);
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
}
