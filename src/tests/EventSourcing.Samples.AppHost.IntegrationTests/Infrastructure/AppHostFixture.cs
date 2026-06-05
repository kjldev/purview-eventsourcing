using System.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Aspire;

namespace Purview.EventSourcing.Samples.AppHost.Infrastructure;

public sealed class AppHostFixture : AspireFixture<Program>
{
	readonly string _databaseName;
	string? _databaseConnectionString;

	IServiceProvider? _serviceProvider;
	readonly ServiceCollection _services = new();

	public AppHostFixture()
	{
		_databaseName = $"EventSourcingSampleTest_" + $"{Guid.NewGuid():N}"[..12];
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();

		_databaseConnectionString = BuildDatabaseConnectionString(
			await GetConnectionStringAsync("sql")
				?? throw new InvalidOperationException("The AppHost did not expose a database connection string.")
		);

		await WaitForWebAppAsync(CancellationToken.None);

		_services.AddSqlServerSnapshotQueryableEventStore(registerAsIEventStore: true);

		_serviceProvider = _services.BuildServiceProvider(
			new ServiceProviderOptions() { ValidateOnBuild = true, ValidateScopes = true }
		);
	}

	public HttpClient CreateWebClient() => CreateHttpClient("web", "http");

	async Task WaitForWebAppAsync(CancellationToken cancellationToken)
	{
		using var client = CreateWebClient();
		client.Timeout = TimeSpan.FromSeconds(10);

		var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(3);
		while (DateTimeOffset.UtcNow < timeoutAt)
		{
			try
			{
				using var response = await client.GetAsync("/pingz/", cancellationToken);
				if (response.IsSuccessStatusCode)
					return;
			}
			catch (Exception) when (DateTimeOffset.UtcNow < timeoutAt)
			{
				// Resource may still be starting.
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

	public IQueryableEventStore<T> QueryableEventStore<T>()
	{
		if (_databaseConnectionString is null)
			throw new InvalidOperationException("The database connection string is not available.");
	}
}
