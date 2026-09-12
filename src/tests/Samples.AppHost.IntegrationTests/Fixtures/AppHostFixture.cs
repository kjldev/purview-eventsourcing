using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.Aspire.ResourceKit;
using Purview.EventSourcing.Fixtures;
using TUnit.Aspire;

namespace Purview.EventSourcing.Samples.Fixtures;

public sealed class AppHostFixture : AspireFixture<Projects.Samples_AppHost>, IServiceProvider
{
	readonly string _databaseName = $"EventStoreSample_" + $"{Guid.NewGuid():N}"[..8];
	readonly string _snapshotBlobName = $"es-snapshot-" + $"{Guid.NewGuid():N}"[..8];
	readonly Lazy<AppServiceHelper> _appService;

	string? _databaseConnectionString;

	protected override AspireFixtureOptions Options => new() { RetainResourceLogs = true };

	public AppHostFixture()
	{
		EventStoreOperationContext.RequiresValidPrincipalIdentifierDefault = false;

		_appService = new(() => new(ConfigureAppServiceHelper));
	}

	protected override string[] Args =>
		OptionsHelper
			.Assign<AppHost.AppModel.SampleAppHostKit.SampleAppHostKitOptions>(
				c => c.IsTestRun = true,
				c => c.IsLocal = false,
				c => c.SqlServer.DatabaseName = _databaseName,
				c => c.AzureStorage.BlobName = _snapshotBlobName
			)
			.Build();

	public override async ValueTask DisposeAsync()
	{
		if (_appService.IsValueCreated)
			await _appService.Value.DisposeAsync();

		await base.DisposeAsync();
	}

	void ConfigureAppServiceHelper(IServiceCollection services, IConfigurationBuilder configurationBuilder)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(_databaseConnectionString);

		services
			// The event stores...
			.AddSqlServerEventStore(Platform.SqlDatabase)
			.AddSqlServerSnapshotQueryableEventStore(Platform.SqlDatabase)
			// Domain services...
			.AddDomainServices();

		configurationBuilder.AddInMemoryCollection([
			new KeyValuePair<string, string?>($"ConnectionStrings:{Platform.SqlDatabase}", _databaseConnectionString),
		]);
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		_databaseConnectionString = await GetConnectionStringAsync(Platform.SqlDatabase);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA5399:Do not use HttpClientHandler.AllowAutoRedirect"
	)]
	public HttpClient CreateWebClient(bool followRedirects = false) =>
		CreateWebClient(Platform.SqlWebApp, followRedirects);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
	public HttpClient CreateWebClient(string resourceName, bool followRedirects = false)
	{
		var httpClient = CreateHttpClient(resourceName, "http");
		if (followRedirects)
			return httpClient;

		// We want auto redirect disabled for tests to be able to assert on 302 responses,
		// but HttpClient doesn't allow changing that setting after the client is created,
		// so we create a new client with the same base address and a handler that has auto redirect disabled.
		return new(new HttpClientHandler() { AllowAutoRedirect = false, CheckCertificateRevocationList = true })
		{
			BaseAddress = httpClient.BaseAddress,
		};
	}

	public Task<string?> GetResourceConnectionStringAsync(string resourceName, CancellationToken cancellationToken) =>
		GetConnectionStringAsync(resourceName, cancellationToken);

	public IQueryableEventStore QueryableEventStore() => _appService.Value.GetRequiredService<IQueryableEventStore>();

	public IEventStore EventStore() => _appService.Value.GetRequiredService<IEventStore>();

	public object? GetService(Type serviceType) => _appService.Value.GetService(serviceType);

	public IServiceProvider CloneServices(Action<IServiceCollection>? configure) =>
		_appService.Value.CloneServices(configure);
}
