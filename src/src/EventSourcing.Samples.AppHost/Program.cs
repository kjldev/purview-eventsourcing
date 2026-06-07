using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

//builder.Services.AddOptionsWithValidateOnStart<AppHostOptions>(AppHostOptions.SectionName);

var databaseName = args
    ?.FirstOrDefault(s => s.StartsWith("--DatabaseName=", StringComparison.OrdinalIgnoreCase))
    ?.Split('=')
    .LastOrDefault();

var isTesting =
    args?.FirstOrDefault(s => s.Equals("--IsTestRun", StringComparison.OrdinalIgnoreCase)) != null;

var sqlPassword = builder.AddParameter("sql-password", "PaSsw0rd!!1!", secret: true);
var sql = builder.AddSqlServer("sql", password: sqlPassword).WithImageTag("2025-latest");

if (!isTesting)
    sql.WithDataVolume("eventsourcing-sample-sql-data");

var db = sql.AddDatabase("eventstore-sqlserver", databaseName);

var redis = builder.AddRedis("redis");
redis.WithRedisInsight(c => c.WithParentRelationship(redis));

var blobs = builder
    .AddAzureStorage("storage")
    .RunAsEmulator(e =>
    {
        if (!isTesting)
            e.WithDataVolume("eventsourcing-sample-azurite-data");
    })
    .AddBlobs("blob-storage");

builder
    .AddProject<Projects.EventSourcing_Samples_Web>("web")
    .WithReference(db)
    .WaitFor(db)
    .WithReference(redis)
    .WaitFor(redis)
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithExternalHttpEndpoints();

builder.Build().Run();
