using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

var databaseName =
	args?.FirstOrDefault(static s => s.StartsWith("--DatabaseName=", StringComparison.OrdinalIgnoreCase))
		?.Split('=')
		.LastOrDefault()
	?? "EventSourcingSampleDb";

var isTesting = args?.FirstOrDefault(static s => s.Equals("--IsTestRun", StringComparison.OrdinalIgnoreCase)) != null;

if (!isTesting)
	builder.AddAspireC4();

var sqlPassword = builder.AddParameter("sql-password", "PaSsw0rd!!1!", secret: true);
var sql = builder.AddSqlServer("sql", password: sqlPassword).WithImageTag("2025-latest");
var db = sql.AddDatabase("eventstore-sqlserver", databaseName);

var blobs = builder
	.AddAzureStorage("storage")
	.RunAsEmulator(e =>
	{
		if (!isTesting)
			e.WithDataVolume("eventsourcing-sample-azurite-data");
	})
	.AddBlobs(isTesting ? $"ess-{Guid.NewGuid():N}"[..8] : "blob-storage");

IResourceBuilder<RedisResource>? redis = null;
if (!isTesting)
{
	redis = builder.AddRedis("redis");
	redis.WithRedisInsight(c => c.WithParentRelationship(redis));

	sql.WithDataVolume("eventsourcing-sample-sql-data");
}

var web = builder
	.AddProject<Projects.EventSourcing_Samples_Web>("web")
	.WithReference(db)
	.WaitFor(db)
	.WithReference(blobs)
	.WaitFor(blobs)
	.WithExternalHttpEndpoints();

if (!isTesting)
{
	web.WithReference(redis!).WaitFor(redis!);
}

var app = builder.Build();

await app.RunAsync();
