using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Purview.EventSourcing.Samples.Fixtures;

namespace Purview.EventSourcing.Samples.Web.Infrastructure;

[NotInParallel("SamplesAppHost")]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class AzureStorageConnectionStringTests(AppHostFixture fixture)
{
	[Test]
	public async Task AzureTableResourceConnection_ContainsTableEndpoint(CancellationToken cancellationToken)
	{
		var connectionString = await fixture.GetResourceConnectionStringAsync(
			Platform.AzureStorageTable,
			cancellationToken
		);

		await Assert.That(connectionString).IsNotNull();
		await Assert.That(connectionString!).Contains("TableEndpoint=");
	}

	[Test]
	public async Task AzureBlobResourceConnection_ContainsBlobEndpoint(CancellationToken cancellationToken)
	{
		var connectionString = await fixture.GetResourceConnectionStringAsync(
			Platform.AzureStorageBlob,
			cancellationToken
		);

		await Assert.That(connectionString).IsNotNull();
		await Assert.That(connectionString!).Contains("BlobEndpoint=");
	}

	[Test]
	public async Task AzureBlobResourceConnection_CanCreateBlobServiceClient(CancellationToken cancellationToken)
	{
		var connectionString = await fixture.GetResourceConnectionStringAsync(
			Platform.AzureStorageBlob,
			cancellationToken
		);

		await Assert.That(connectionString).IsNotNull();

		var normalized = AzureStorageConnectionStringComposer.Normalize(connectionString);
		var containerName = $"startupcheck-{Guid.NewGuid():N}"[..22];
		BlobServiceClient serviceClient = new(normalized);
		var containerClient = serviceClient.GetBlobContainerClient(containerName);
		await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
		await containerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	}

	[Test]
	public async Task AzureMergedEventStoreConnection_CanCreateBlobAndTableClients(CancellationToken cancellationToken)
	{
		var tableConnectionString = await fixture.GetResourceConnectionStringAsync(
			Platform.AzureStorageTable,
			cancellationToken
		);
		var blobConnectionString = await fixture.GetResourceConnectionStringAsync(
			Platform.AzureStorageBlob,
			cancellationToken
		);

		await Assert.That(tableConnectionString).IsNotNull();
		await Assert.That(blobConnectionString).IsNotNull();

		var mergedConnectionString = AzureStorageConnectionStringComposer.BuildEventStoreConnectionString(
			tableConnectionString,
			blobConnectionString,
			fallbackConnectionString: null
		);

		var blobContainerName = $"startupcheck-{Guid.NewGuid():N}"[..22];
		BlobContainerClient blobClient = new(mergedConnectionString, blobContainerName);
		await blobClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
		await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

		var tableName = $"StartupCheck{Guid.NewGuid():N}"[..24];
		var tableClient = new TableServiceClient(mergedConnectionString).GetTableClient(tableName);
		await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
		await tableClient.DeleteAsync(cancellationToken);
	}
}
