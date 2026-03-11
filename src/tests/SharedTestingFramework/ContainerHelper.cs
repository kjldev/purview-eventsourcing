using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace Purview.EventSourcing;

public static class ContainerHelper
{
	public static AzuriteContainer CreateAzurite(Action<AzuriteBuilder>? config = null)
	{
		var builder = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0");
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
		//.WithAutoRemove(true)
		//.WithCleanUp(true)
		//.WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "5")
		//.WithEnvironment("AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE", "127.0.0.1")
		.WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false")
		//.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8081))
		;

		config?.Invoke(builder);

		return builder.Build();
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
