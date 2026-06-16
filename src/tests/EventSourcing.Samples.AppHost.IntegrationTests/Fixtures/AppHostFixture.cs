using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Fixtures;
using TUnit.Aspire;

namespace Purview.EventSourcing.Samples.AppHost.Fixtures;

public sealed class AppHostFixture : AspireFixture<Program>, IServiceProvider
{
	readonly string _databaseName = $"EventSourcingSampleTest_" + $"{Guid.NewGuid():N}"[..8];
	readonly Lazy<AppServiceHelper> _appService;

	string? _databaseConnectionString;

	public AppHostFixture()
	{
		_appService = new(() => new(ConfigureAppServiceHelper));
	}

	protected override string[] Args => [$"--DatabaseName={_databaseName}", "--IsTestRun"];

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
			.AddSqlServerEventStore()
			.AddSqlServerSnapshotQueryableEventStore()
			// Domain services...
			.AddDomainServices();

		configurationBuilder.AddInMemoryCollection([
			new KeyValuePair<string, string?>("ConnectionStrings:SqlServer", _databaseConnectionString),
		]);
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		_databaseConnectionString = BuildDatabaseConnectionString(
			await GetConnectionStringAsync("sql")
				?? throw new InvalidOperationException("The AppHost did not expose a database connection string.")
		);

		await WaitForWebAppAsync(CancellationToken.None);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA5399:Do not use HttpClientHandler.AllowAutoRedirect"
	)]
	public HttpClient CreateWebClient()
	{
		var httpClient = CreateHttpClient("web", "http");

		// We want auto redirect disabled for tests to be able to assert on 302 responses,
		// but HttpClient doesn't allow changing that setting after the client is created,
		// so we create a new client with the same base address and a handler that has auto redirect disabled.
		return new(new HttpClientHandler() { AllowAutoRedirect = false }) { BaseAddress = httpClient.BaseAddress };
	}

	async Task WaitForWebAppAsync(CancellationToken cancellationToken)
	{
		using var client = CreateWebClient();
		client.Timeout = TimeSpan.FromSeconds(10);

		var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(3);
		while (DateTimeOffset.UtcNow < timeoutAt)
		{
			try
			{
				using var response = await client.GetAsync("/pingz", cancellationToken);

				if (TestContext.Current != null)
					await TestContext.Current.OutputWriter.WriteLineAsync("Pingz Response: " + response.StatusCode);

				Console.WriteLine("Pingz Response: " + response.StatusCode);

				if (response.IsSuccessStatusCode)
					return;
			}
			catch (Exception ex) when (DateTimeOffset.UtcNow < timeoutAt)
			{
				// Resource may still be starting.
				if (TestContext.Current != null)
					await TestContext.Current.OutputWriter.WriteLineAsync("Waiting for Web Failure: " + ex.Message);

				Console.WriteLine("Waiting for Web Failure: " + ex.Message);
			}

			await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
		}

		throw new InvalidOperationException("The web app resource did not become ready in time.");
	}

	string BuildDatabaseConnectionString(string connectionString)
	{
		SqlConnectionStringBuilder builder = new(connectionString) { InitialCatalog = _databaseName };

		return builder.ConnectionString;
	}

	public IQueryableEventStore QueryableEventStore() => _appService.Value.GetRequiredService<IQueryableEventStore>();

	public IEventStore EventStore() => _appService.Value.GetRequiredService<IEventStore>();

	public object? GetService(Type serviceType) => _appService.Value.GetService(serviceType);

	public IServiceProvider CloneServices(Action<IServiceCollection>? configure) =>
		_appService.Value.CloneServices(configure);
}
