using System.Diagnostics;
using Azure;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Npgsql;
using Purview.EventSourcing.Samples.Options;
using Purview.EventSourcing.Samples.Services;
using AzureCommitException = Purview.EventSourcing.AzureStorage.Exceptions.CommitException;
using AzureConcurrencyException = Purview.EventSourcing.AzureStorage.Exceptions.ConcurrencyException;
using SqlServerCommitException = Purview.EventSourcing.SqlServer.Events.Exceptions.CommitException;
using SqlServerConcurrencyException = Purview.EventSourcing.SqlServer.Events.Exceptions.ConcurrencyException;

namespace Purview.EventSourcing.Samples.Web.Services;

sealed class SampleSeedService(
	ISeedDataService seedDataService,
	IOptions<SampleStoreOptions> sampleStoreOptions,
	ISampleSeedServiceTelemetry telemetry
)
{
	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			telemetry.Seeding(sampleStoreOptions.Value.CurrentKey, attempt + 1);

			try
			{
				var sw = Stopwatch.StartNew();
				await seedDataService.SeedAsync(cancellationToken);

				sw.Stop();

				telemetry.Complete(sampleStoreOptions.Value.CurrentKey, attempt + 1, sw.Elapsed);

				return;
			}
			catch (SqlServerConcurrencyException) when (attempt < 2)
			{
				// Another app instance may be seeding the demo store at the same time.
				await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
			}
			catch (AzureConcurrencyException) when (attempt < 2)
			{
				// Another app instance may be seeding the demo store at the same time.
				await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), cancellationToken);
			}
			catch (RequestFailedException ex)
			{
				telemetry.RequestFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (MongoException ex)
			{
				telemetry.MongoFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (NpgsqlException ex)
			{
				telemetry.NpgsqlFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (SqlServerCommitException ex)
			{
				telemetry.SqlServerCommitFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (AzureCommitException ex)
			{
				telemetry.AzureCommitFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (FormatException ex)
			{
				telemetry.FormatFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (ArgumentException ex)
			{
				telemetry.ArgumentFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
			catch (InvalidOperationException ex)
			{
				telemetry.InvalidOperationFailed(sampleStoreOptions.Value.CurrentKey, ex);
				break;
			}
		}
	}
}
