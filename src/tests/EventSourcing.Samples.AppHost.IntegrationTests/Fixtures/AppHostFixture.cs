using System.Data.SqlClient;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TUnit.Aspire;

namespace Purview.EventSourcing.Samples.AppHost.Fixtures;

public sealed class AppHostFixture : AspireFixture<Program>, IServiceProvider
{
	readonly string _databaseName = $"EventSourcingSampleTest_" + $"{Guid.NewGuid():N}"[..8];
	readonly ServiceCollection _services = [];

	string? _databaseConnectionString;
	AsyncServiceScope? _serviceScope;

	protected override string[] Args => [$"--DatabaseName={_databaseName}", "--IsTestRun"];

	public IServiceProvider ServiceProvider
	{
		get
		{
			return field is null
				? throw new InvalidOperationException("The fixture has not been initialised yet.")
				: (field);
		}
		private set;
	}

	public override async ValueTask DisposeAsync()
	{
		if (_serviceScope != null)
			await _serviceScope.Value.DisposeAsync();

		await base.DisposeAsync();
	}

	public IServiceCollection ServiceCollection => _services;

	protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
	{
		builder
			.Services
			// The event stores...
			.AddSqlServerEventStore()
			.AddSqlServerSnapshotQueryableEventStore()
			// Event store uses the cache...
			.AddDistributedMemoryCache()
			// Domain services...
			.AddDomainServices();

		builder.Configuration.AddInMemoryCollection([
			new KeyValuePair<string, string?>("ConnectionStrings:SqlServer", _databaseConnectionString),
		]);

		base.ConfigureBuilder(builder);
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		_databaseConnectionString = BuildDatabaseConnectionString(
			await GetConnectionStringAsync("sql")
				?? throw new InvalidOperationException("The AppHost did not expose a database connection string.")
		);

		await WaitForWebAppAsync(CancellationToken.None);

		_serviceScope = App.Services.CreateAsyncScope();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Reliability",
		"CA5399:Do not use HttpClientHandler.AllowAutoRedirect"
	)]
	public HttpClient CreateWebClient()
	{
		var httpClient = CreateHttpClient("web", "http");
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

	public IQueryableEventStore QueryableEventStore()
	{
		return _databaseConnectionString is null
			? throw new InvalidOperationException("The database connection string is not available.")
			: _serviceScope!.Value.ServiceProvider.GetRequiredService<IQueryableEventStore>();
	}

	public IEventStore EventStore()
	{
		return _databaseConnectionString is null
			? throw new InvalidOperationException("The database connection string is not available.")
			: _serviceScope!.Value.ServiceProvider.GetRequiredService<IEventStore>();
	}

	public IServiceCollection CloneServiceCollection(Action<IServiceCollection>? configuration = null)
	{
		ServiceCollection clone = [.. _services];

		configuration?.Invoke(clone);

		return clone;
	}

	public IServiceProvider CloneServiceProvider(Action<IServiceCollection>? configuration = null)
	{
		var services = CloneServiceCollection(configuration);

		return services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
		);
	}

	public object? GetService(Type serviceType) => _serviceScope!.Value.ServiceProvider.GetService(serviceType);
}
