using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Samples.Fixtures;

namespace Purview.EventSourcing.Samples.Web.Infrastructure;

[NotInParallel("SamplesAppHost")]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class VariantSelectionTests(AppHostFixture fixture)
{
	static readonly (string ResourceName, string EventStore, string QueryStore, string AdminStore)[] Variants =
	[
		(Platform.SqlWebApp, "SqlServer", "SqlServer", "SqlServer"),
		(Platform.PostgresWebApp, "Postgres", "Postgres", "Postgres"),
		(Platform.MongoDBWebApp, "MongoDB", "MongoDB", "MongoDB"),
		(Platform.AzureSqlWebApp, "AzureStorage", "SqlServer", "AzureStorage"),
		(Platform.AzurePostgresWebApp, "AzureStorage", "Postgres", "AzureStorage"),
		(Platform.AzureMongoDBWebApp, "AzureStorage", "MongoDB", "AzureStorage"),
	];

	[Test]
	[MethodDataSource(nameof(GetVariants))]
	public async Task Variant_ServesItsConfiguredStores(
		string resourceName,
		string expectedEventStore,
		string expectedQueryStore,
		string expectedAdminStore,
		CancellationToken cancellationToken
	)
	{
		using var client = fixture.CreateWebClient(resourceName, followRedirects: true);

		var html = await GetDashboardHtmlAsync(client, cancellationToken);

		await Assert.That(html).Contains("Customer Experience");
		await Assert.That(html).Contains("Back Office");

		// Each variant must load its own backing stores, not a shared default.
		await Assert.That(html).Contains($"Event: {expectedEventStore}");
		await Assert.That(html).Contains($"Query: {expectedQueryStore}");
		await Assert.That(html).Contains($"Admin: {expectedAdminStore}");
	}

	[Test]
	public async Task WebAppVariants_DoNotShareAFixedDeclaredPort()
	{
		var model = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();

		var webApps = model
			.Resources.OfType<ProjectResource>()
			.Where(resource =>
				resource.Name is not null && resource.Name.StartsWith("web-app", StringComparison.Ordinal)
			)
			.ToArray();

		await Assert.That(webApps.Length).IsEqualTo(Variants.Length);

		// A shared fixed port (e.g. inherited from the launch profile's applicationUrl) makes every
		// variant resolve to the same endpoint in the dashboard, so each variant must rely on a
		// DCP-assigned dynamic port or a port unique to that instance.
		var httpPorts = webApps
			.Select(resource =>
				resource.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == "http")
			)
			.Select(annotation => annotation.Port)
			.Where(port => port is not null)
			.Cast<int>()
			.ToArray();

		await Assert.That(httpPorts.Length).IsEqualTo(httpPorts.Distinct().Count());
	}

	public static IEnumerable<(string, string, string, string)> GetVariants() =>
		Variants.Select(variant => (variant.ResourceName, variant.EventStore, variant.QueryStore, variant.AdminStore));

	static async Task<string> GetDashboardHtmlAsync(HttpClient client, CancellationToken cancellationToken)
	{
		// The Aspire fixture reports the project as running before Kestrel accepts traffic; retry
		// briefly until the dashboard responds successfully.
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

		while (true)
		{
			var response = await client.GetAsync("/", timeoutCts.Token);
			if (response.IsSuccessStatusCode)
				return await response.Content.ReadAsStringAsync(timeoutCts.Token);

			await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token);
		}
	}
}
