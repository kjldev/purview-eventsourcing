using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.Samples.Web.Infrastructure;

public sealed class WebAppFactory : WebApplicationFactory<Program>, IAsyncInitializer, IAsyncDisposable
{
	readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

	public async Task InitializeAsync()
	{
		await _sqlContainer.StartAsync();
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseSetting("ConnectionStrings:eventstore-sqlserver", _sqlContainer.GetConnectionString());

		builder.ConfigureServices(services =>
		{
			// Replace Redis IDistributedCache with in-memory for integration tests
			var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
			if (descriptor is not null)
				services.Remove(descriptor);

			services.AddDistributedMemoryCache();
		});
	}

	async ValueTask IAsyncDisposable.DisposeAsync()
	{
		await _sqlContainer.DisposeAsync();
		await base.DisposeAsync();
	}
}
