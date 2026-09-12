namespace Purview.EventSourcing.Samples;

public static class Platform
{
	public const string AspireC4 = "aspirec4";

	public const string SqlServer = "eventstore-sql";
	public const string SqlDatabase = "eventstore-db";
	public const string SqlSharedQueryDatabase = "eventstore-shared-db";

	public const string Postgres = "eventstore-postgres";
	public const string PostgresDatabase = "eventstore-postgres-db";
	public const string PostgresSharedQueryDatabase = "eventstore-postgres-shared-db";

	public const string MongoDB = "eventstore-mongo";
	public const string MongoDatabase = "eventstore-mongo-db";
	public const string MongoSharedQueryDatabase = "eventstore-mongo-shared-db";

	public const string Redis = "redis";

	public const string AzureStorage = "storage";
	public const string AzureStorageTable = "eventstore-tables";
	public const string AzureStorageBlob = "eventstore-snapshots";

	public const string WebApp = "web-app";
	public const string SqlWebApp = "web-app-sql";
	public const string PostgresWebApp = "web-app-postgres";
	public const string MongoDBWebApp = "web-app-mongo";
	public const string AzureSqlWebApp = "web-app-azure-sql";
	public const string AzurePostgresWebApp = "web-app-azure-postgres";
	public const string AzureMongoDBWebApp = "web-app-azure-mongo";
}
