using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Purview.EventSourcing;

public static partial class ContainerHelper
{
	public static AzuriteContainer CreateAzurite(Action<AzuriteBuilder>? config = null)
	{
		// Note: Testcontainers.Azurite's default command already binds the emulator to 0.0.0.0 and
		// WithCommand appends to that default. Do not repeat the --blobHost/--queueHost/--tableHost
		// flags here: duplicated flags make Azurite fall back to binding 127.0.0.1, which is not
		// reachable through Docker's published ports.
		var builder = new AzuriteBuilder($"mcr.microsoft.com/azure-storage/azurite:{AzuriteImageTag}").WithCommand(
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
		var builder = new CosmosDbBuilder($"mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:{CosmosDbImageTag}")
			.WithWaitStrategy(
				Wait.ForUnixContainer()
					.AddCustomWaitStrategy(new CosmosDbWaitUntil(), ws => ws.WithTimeout(TimeSpan.FromMinutes(5)))
			)
			//.WithAutoRemove(true)
			//.WithCleanUp(true)
			// The emulator must serve plain HTTP: Testcontainers' connection string and the Cosmos SDK
			// gateway reader both use http:// (the PROTOCOL=https emulator only serves HTTPS and is
			// unreachable over the published port).
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
			// The emulator serves plain HTTP on the gateway; probe it the same way the Cosmos SDK's
			// gateway reader connects (via the published port).
			var endpoint = new UriBuilder(
				Uri.UriSchemeHttp,
				container.Hostname,
				container.GetMappedPublicPort(CosmosDbBuilder.CosmosDbPort)
			).Uri;

			using HttpClient httpClient = new();
			try
			{
				using var httpResponse = await httpClient.GetAsync(endpoint).ConfigureAwait(false);
				return httpResponse.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
			}
#pragma warning disable CA1031
			catch
#pragma warning restore CA1031
			{
				return false;
			}
		}
	}

	public static MongoDbContainer CreateMongoDB(Action<MongoDbBuilder>? config = null)
	{
		var builder = new MongoDbBuilder($"mongo:{MongoDBImageTag}").WithReplicaSet()
		//.WithAutoRemove(true)
		//.WithCleanUp(true)
		//.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
		;

		config?.Invoke(builder);

		return builder.Build();
	}

	public static MsSqlContainer CreateMsSql(Action<MsSqlBuilder>? config = null)
	{
		MsSqlBuilder builder = new($"mcr.microsoft.com/mssql/server:{SqlServerImageTag}");

		config?.Invoke(builder);

		return builder.Build();
	}

	public static PostgreSqlContainer CreatePostgreSql(Action<PostgreSqlBuilder>? config = null)
	{
		PostgreSqlBuilder builder = new($"postgres:{PostgresImageTag}");

		config?.Invoke(builder);

		return builder.Build();
	}
}
