var builder = DistributedApplication.CreateBuilder(args);

var databaseName = args.Length > 0 ? args[0] : "EventSourcingSample";

var sqlPassword = builder.AddParameter("sql-password", "PaSsw0rd!!1!", secret: true);

var sql = builder
    .AddSqlServer("sql", password: sqlPassword)
    .WithImageTag("2025-latest")
    .WithDataVolume("eventsourcing-sample-sql-data");

var db = sql.AddDatabase("eventstore-sqlserver", databaseName);

var redis = builder.AddRedis("redis").WithDataVolume("eventsourcing-sample-redis-data");
redis.WithRedisInsight(c => c.WithParentRelationship(redis));

var blobs = builder
    .AddAzureStorage("storage")
    .RunAsEmulator(e => e.WithDataVolume("eventsourcing-sample-azurite-data"))
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
