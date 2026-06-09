using TUnit.Core.Interfaces;

namespace Purview.EventSourcing.Fixtures;

public class CosmosDbSnapshotEventStoreFixture : IAsyncInitializer, IAsyncDisposable
{
	readonly Testcontainers.Azurite.AzuriteContainer _azuriteContainer;
	readonly Testcontainers.CosmosDb.CosmosDbContainer _cosmosDbContainer;

	public CosmosDbSnapshotEventStoreFixture()
	{
		_azuriteContainer = ContainerHelper.CreateAzurite();
		_cosmosDbContainer = ContainerHelper.CreateCosmosDB();
	}

	public CosmosDbSnapshotEventStoreContext CreateContext(int correlationIdsToGenerate = 1)
	{
		CosmosDbSnapshotEventStoreContext eventStoreContext = new(
			_cosmosDbContainer.GetConnectionString(),
			_cosmosDbContainer.HttpClient,
			_azuriteContainer.GetConnectionString()
		);

		eventStoreContext.CreateCosmosDbEventStore(correlationIdsToGenerate: correlationIdsToGenerate);

		return eventStoreContext;
	}

	public async ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);

		await _azuriteContainer.DisposeAsync().AsTask();
		await _cosmosDbContainer.DisposeAsync().AsTask();
	}

	public async Task InitializeAsync()
	{
		await _azuriteContainer.StartAsync();
		await _cosmosDbContainer.StartAsync();
	}
}
