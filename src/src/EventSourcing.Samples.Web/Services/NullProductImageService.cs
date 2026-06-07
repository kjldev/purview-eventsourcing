namespace Purview.EventSourcing.Samples.Web.Services;

sealed class NullProductImageService : IProductImageService
{
	public Task<string?> GetImageUrlAsync(
		string productId,
		CancellationToken cancellationToken = default
	) => Task.FromResult<string?>(null);

	public Task UploadImageAsync(
		string productId,
		Stream imageStream,
		string contentType,
		CancellationToken cancellationToken = default
	) => Task.CompletedTask;

	public Task DeleteImageAsync(string productId, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}
