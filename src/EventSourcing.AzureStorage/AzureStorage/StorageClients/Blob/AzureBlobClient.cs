using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;

namespace Purview.EventSourcing.AzureStorage.StorageClients.Blob;

sealed class AzureBlobClient
{
	readonly AsyncLazy<BlobContainerClient> _blobContainerClient;

	readonly AzureStorageEventStoreOptions _configuration;
	readonly string? _containerName;

	public AzureBlobClient(AzureStorageEventStoreOptions configuration, string? containerOverride = null)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

		_containerName = containerOverride ?? _configuration.Container;
		_blobContainerClient = new AsyncLazy<BlobContainerClient>(InitializeAsync);
	}

	public async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient;
	}

	public async Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		var blobClient = containerClient.GetBlockBlobClient(blobName);

		return await blobClient.ExistsAsync(cancellationToken);
	}

	public async Task<T?> GetAsAsync<T>(string blobName, CancellationToken cancellationToken = default)
		where T : class
	{
		var responseStream = await GetStreamAsync(blobName, cancellationToken);
		if (responseStream == null)
			return null;

		string content;
		using (StreamReader streamReader = new(responseStream))
			content = await streamReader.ReadToEndAsync(cancellationToken);

		return JsonConvert.DeserializeObject<T>(content);
	}

	public async Task<Stream?> GetStreamAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var blobClient = await GetBlobClientAsync(blobName, cancellationToken);
		var response = await blobClient.DownloadAsync(cancellationToken);

		return response.Value.Content;
	}

	public async Task<BlobProperties> GetBlobPropertiesAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var blobClient = await GetBlobClientAsync(blobName, cancellationToken);
		return await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
	}

	public async Task<BlobContainerProperties> GetPropertiesAsync(CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		return await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
	}

	public async Task<BlobInfo> SetMetadataAsync(string blobName, IDictionary<string, string> metadata, CancellationToken cancellationToken = default)
	{
		var blobClient = await GetBlobClientAsync(blobName, cancellationToken);
		return await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
	}

	public async Task<BlobContainerInfo> SetMetadataAsync(IDictionary<string, string> metadata, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		return await containerClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
	}

	public async Task<BlobClient> GetBlobClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetBlobClient(blobName);
	}

	public async Task<AppendBlobClient> GetAppendBlobClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetAppendBlobClient(blobName);
	}

	public async Task<PageBlobClient> GetPageBlobClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetPageBlobClient(blobName);
	}

	public async Task<BlockBlobClient> GetBlockBlobClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetBlockBlobClient(blobName);
	}

	public async Task<BlobLeaseClient> GetBlobLeaseClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetBlobLeaseClient(blobName);
	}

	public async Task<BlobBaseClient> GetBlobBaseClientAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;

		cancellationToken.ThrowIfCancellationRequested();

		return containerClient.GetBlobBaseClient(blobName);
	}

	public async Task<BlobContentInfo> UploadAsync(string blobName, Stream content, bool overwrite = false, CancellationToken cancellationToken = default)
	{
		var blobClient = await GetBlobClientAsync(blobName, cancellationToken);
		var blobContent = await blobClient.UploadAsync(content, overwrite: overwrite, cancellationToken: cancellationToken);

		return blobContent;
	}

	public async Task<BlobContentInfo> UploadAsync(string blobName, Stream content, IDictionary<string, string> metadata, bool overwrite = false, CancellationToken cancellationToken = default)
	{
		var blobClient = await GetBlobClientAsync(blobName, cancellationToken);
		return await blobClient.UploadAsync(content, metadata: metadata, conditions: overwrite ? null : new BlobRequestConditions
		{
			IfNoneMatch = new ETag("*")
		}, cancellationToken: cancellationToken);
	}

	public async Task<bool> DeleteContainerAsync(CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		var result = await containerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

		return result?.Value ?? true;
	}

	public async Task<bool> DeleteBlobIfExistsAsync(string blobName, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		return await containerClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
	}

	public async Task<AsyncPageable<BlobItem>> GetBlobsAsync(string? prefix = null, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		return containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);
	}

	public async Task<AsyncPageable<BlobHierarchyItem>> GetBlobsByHierarchyAsync(string? delimiter = null, string? prefix = null, CancellationToken cancellationToken = default)
	{
		var containerClient = await _blobContainerClient;
		return containerClient.GetBlobsByHierarchyAsync(delimiter: delimiter, prefix: prefix, cancellationToken: cancellationToken);
	}

	public async Task<BlobContainerInfo?> CreateContainerIfNotExistsAsync(IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
	{
		var containerClient = CreateContainerClient();
		var response = await containerClient.CreateIfNotExistsAsync(
			publicAccessType: PublicAccessType.None,
			metadata: metadata,
			cancellationToken: cancellationToken
		);

		return response?.Value;
	}

	async Task<BlobContainerClient> InitializeAsync(CancellationToken cancellationToken = default)
	{
		var blobContainerClient = CreateContainerClient();
		await CreateContainerIfNotExistsAsync(cancellationToken: cancellationToken);

		return blobContainerClient;
	}

	BlobContainerClient CreateContainerClient()
	{
		BlobClientOptions clientOptions = new();
		if (_configuration.TimeoutInSeconds != null)
			clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(_configuration.TimeoutInSeconds.Value);

		return new(_configuration.ConnectionString, _containerName, clientOptions);
	}
}
