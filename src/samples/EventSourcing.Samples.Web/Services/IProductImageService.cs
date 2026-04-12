namespace Purview.EventSourcing.Samples.Web.Services;

public interface IProductImageService
{
	Task<string?> GetImageUrlAsync(string productId, CancellationToken cancellationToken = default);
	Task UploadImageAsync(string productId, Stream imageStream, string contentType, CancellationToken cancellationToken = default);
	Task DeleteImageAsync(string productId, CancellationToken cancellationToken = default);
}
