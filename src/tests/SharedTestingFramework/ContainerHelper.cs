using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace Purview.EventSourcing;

public static class ContainerHelper
{
	public static AzuriteContainer CreateAzurite(Action<AzuriteBuilder>? config = null)
	{
		var builder = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0").WithCommand(
			"--skipApiVersionCheck"
		)
		//.WithWaitStrategy(Wait.ForUnixContainer()
		//	.UntilPortIsAvailable(10000) // Blob
		//	.UntilPortIsAvailable(10001) // Queue
		//	.UntilPortIsAvailable(10002) // Table
		//)
		;

		config?.Invoke(builder);

		return builder.Build();
	}

	public static CosmosDbContainer CreateCosmosDB(Action<CosmosDbBuilder>? config = null)
	{
		var builder = new CosmosDbBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
			.WithWaitStrategy(
				Wait.ForUnixContainer()
					.AddCustomWaitStrategy(new CosmosDbWaitUntil(), ws => ws.WithTimeout(TimeSpan.FromMinutes(5)))
			)
			//.WithAutoRemove(true)
			//.WithCleanUp(true)
			.WithEnvironment("PROTOCOL", "https")
			.WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false");

		config?.Invoke(builder);

		return builder.Build();
	}

	// The vnext-preview CosmosDB emulator's HTTP server starts before the data engine.
	// A 503 "pgcosmos extension is still starting" means the gateway is up but not ready.
	// Wait until we get any response other than 503.
	sealed class CosmosDbWaitUntil : IWaitUntil
	{
		public async Task<bool> UntilAsync(IContainer container)
		{
			// Root endpoint — returns 401 (no auth) once data engine is ready, 503 before.
			const string requestUri = "https://localhost/";
			var httpClient = ((CosmosDbContainer)container).HttpClient;
			try
			{
				using var httpResponse = await httpClient.GetAsync(requestUri).ConfigureAwait(false);
				return httpResponse.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
			}
#pragma warning disable CA1031
			catch
#pragma warning restore CA1031
			{
				return false;
			}
			finally
			{
				httpClient.Dispose();
			}
		}
	}

	public static MongoDbContainer CreateMongoDB(Action<MongoDbBuilder>? config = null)
	{
		var builder = new MongoDbBuilder("mongo:7.0").WithReplicaSet()
		//.WithAutoRemove(true)
		//.WithCleanUp(true)
		//.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
		;

		config?.Invoke(builder);

		return builder.Build();
	}

	public static MsSqlContainer CreateMsSql(Action<MsSqlBuilder>? config = null)
	{
		var builder = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest");

		config?.Invoke(builder);

		return builder.Build();
	}
}
