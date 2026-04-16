namespace Purview.EventSourcing.Samples.Services;

public interface ISeedDataService
{
	Task SeedAsync(CancellationToken cancellationToken = default);
}
