var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", "PaSsw0rd!!1!", secret: true);

var sql = builder
	.AddSqlServer("sql", password: sqlPassword)
	.WithImageTag("2025-latest")
	.WithDataVolume("eventsourcing-sample-sql-data");

var db = sql.AddDatabase("eventstore-sqlserver", "EventSourcingSample");

var redis = builder
	.AddRedis("redis")
	.WithRedisInsight()
	.WithDataVolume("eventsourcing-sample-redis-data");

builder.AddProject<Projects.EventSourcing_Samples_Web>("web")
	.WithReference(db).WaitFor(db)
	.WithReference(redis).WaitFor(redis)
	.WithExternalHttpEndpoints();

builder.Build().Run();
