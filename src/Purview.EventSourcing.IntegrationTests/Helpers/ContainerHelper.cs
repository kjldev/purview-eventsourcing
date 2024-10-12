using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Testcontainers.MongoDb;

namespace Purview.EventSourcing;

public static class ContainerHelper
{
	public static AzuriteContainer CreateAzurite(Action<AzuriteBuilder>? config = null)
	{
		var builder = new AzuriteBuilder()
			.WithImage("mcr.microsoft.com/azure-storage/azurite:3.32.0")
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
		var builder = new CosmosDbBuilder()
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
		var builder = new MongoDbBuilder()
			.WithReplicaSet()
		//.WithAutoRemove(true)
		//.WithCleanUp(true)
		//.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(27017))
		;

		config?.Invoke(builder);

		return builder.Build();
	}
}
