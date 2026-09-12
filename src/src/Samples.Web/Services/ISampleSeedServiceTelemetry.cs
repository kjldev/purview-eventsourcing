using Azure;
using MongoDB.Driver;
using Npgsql;
using Purview.EventSourcing.AzureStorage.Exceptions;
using Purview.Telemetry;

namespace Purview.EventSourcing.Samples.Web.Services;

[Logger]
interface ISampleSeedServiceTelemetry
{
	[Info]
	void Seeding(string currentKey, int attempt);

	[Info]
	void Complete(string currentKey, int attempt, TimeSpan duration);

	[Error]
	void ArgumentFailed(string currentKey, ArgumentException ex);

	[Error]
	void AzureCommitFailed(string currentKey, CommitException ex);

	[Error]
	void FormatFailed(string currentKey, FormatException ex);

	[Error]
	void InvalidOperationFailed(string currentKey, InvalidOperationException ex);

	[Error]
	void MongoFailed(string currentKey, MongoException ex);

	[Error]
	void NpgsqlFailed(string currentKey, NpgsqlException ex);

	[Error]
	void RequestFailed(string currentKey, RequestFailedException ex);

	[Error]
	void SqlServerCommitFailed(string currentKey, SqlServer.Events.Exceptions.CommitException ex);
}
