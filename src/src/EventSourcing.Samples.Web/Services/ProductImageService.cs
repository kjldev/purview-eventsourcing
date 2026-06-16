using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Purview.EventSourcing.Samples.Web.Services;

sealed class ProductImageService(BlobServiceClient blobServiceClient) : IProductImageService
{
	const string ContainerName = "product-images";

	public async Task<string?> GetImageUrlAsync(string productId, CancellationToken cancellationToken = default)
	{
#pragma warning disable CA1031 // Do not catch general exception types
		try
		{
			var container = blobServiceClient.GetBlobContainerClient(ContainerName);
			var blob = container.GetBlobClient(productId);
			return await blob.ExistsAsync(cancellationToken) ? blob.Uri.AbsoluteUri : null;
		}
		catch
		{
			return null;
		}
#pragma warning restore CA1031 // Do not catch general exception types
	}

	public async Task UploadImageAsync(
		string productId,
		Stream imageStream,
		string contentType,
		CancellationToken cancellationToken = default
	)
	{
		var container = blobServiceClient.GetBlobContainerClient(ContainerName);
		await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
		var blob = container.GetBlobClient(productId);
		await blob.UploadAsync(
			imageStream,
			new BlobHttpHeaders { ContentType = contentType },
			cancellationToken: cancellationToken
		);
	}

	public async Task DeleteImageAsync(string productId, CancellationToken cancellationToken = default)
	{
		var container = blobServiceClient.GetBlobContainerClient(ContainerName);
		var blob = container.GetBlobClient(productId);
		await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	}
}
