using Purview.Aspire.ResourceKit;
using Purview.EventSourcing.Samples.Options;

namespace Purview.EventSourcing.Samples.AppHost.AppModel.Resources;

partial class WebAppKit
{
	//static void ValidateConfiguration(WebAppKitOptions options)
	//{
	//	Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);

	//	for (var index = 0; index < options.Variants.Count; index++)
	//	{
	//		var variant = options.Variants[index];
	//		try
	//		{
	//			Validator.ValidateObject(variant, new ValidationContext(variant), validateAllProperties: true);
	//		}
	//		catch (ValidationException ex)
	//		{
	//			throw new InvalidOperationException(
	//				$"Sample web project variant at index {index} is invalid: {ex.ValidationResult?.ErrorMessage ?? ex.Message}",
	//				ex
	//			);
	//		}
	//	}
	//}

	void ConfigureVariant(
		IResourceBuilder<ProjectResource> variant,
		string variantKey,
		VariantConfiguration configuration
	)
	{
		ConfigureStoreReferences(variant, HostKit, configuration);

		if (HostKit.Redis.IsEnabled)
			variant.WithReference(HostKit.Redis).WaitFor(HostKit.Redis);

		if (HostKit.AzureStorage.IsEnabled)
			variant.WithReference(HostKit.AzureStorage.SnapshotBlob).WaitFor(HostKit.AzureStorage.SnapshotBlob);

		variant.WithEnvironment(
			OptionsHelper.Assign<SampleStoreOptions>(
				c => c.CurrentKey = variantKey,
				c => c.CurrentDisplayName = configuration.DisplayName,
				c => c.CurrentDescription = configuration.Description,
				c => c.EventStore = configuration.EventStore,
				c => c.QueryStore = configuration.QueryStore,
				c => c.AdminStore = configuration.AdminStore,
				c => c.AdminAPIAvailable = configuration.AdminAPIAvailable,
				c => c.EventStoreConnectionName = configuration.EventStoreConnectionName,
				c => c.QueryStoreConnectionName = configuration.QueryStoreConnectionName,
				c => c.EventStoreDatabaseName = configuration.EventStoreDatabaseName,
				c => c.QueryStoreDatabaseName = configuration.QueryStoreDatabaseName,
				c => c.AdminDatabaseName = configuration.AdminDatabaseName,
				c => c.AdminSitePath = HostKit.WebApp.Options.AdminSitePath,
				c => c.AdminAPIPath = HostKit.WebApp.Options.AdminAPIPath,
				c => c.DataIsolationWarning = HostKit.WebApp.Options.DataIsolationWarning
			)
		);

		if (!string.IsNullOrWhiteSpace(configuration.EventStoreDatabaseName))
		{
			variant.WithEnvironment(
				OptionsHelper.Assign<SampleStoreOptions>(c =>
					c.EventStoreDatabaseName = configuration.EventStoreDatabaseName
				)
			);
		}

		if (!string.IsNullOrWhiteSpace(configuration.QueryStoreDatabaseName))
		{
			variant.WithEnvironment(
				OptionsHelper.Assign<SampleStoreOptions>(c =>
					c.QueryStoreDatabaseName = configuration.QueryStoreDatabaseName
				)
			);
		}

		if (!string.IsNullOrWhiteSpace(configuration.AdminDatabaseName))
		{
			variant.WithEnvironment(
				OptionsHelper.Assign<SampleStoreOptions>(c => c.AdminDatabaseName = configuration.AdminDatabaseName)
			);
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0010:Add missing cases")]
	static void ConfigureStoreReferences(
		IResourceBuilder<ProjectResource> variant,
		SampleAppHostKit hostKit,
		VariantConfiguration configuration
	)
	{
		var mongoReferenced = false;

		switch (configuration.EventStore)
		{
			case SampleEventStoreKind.SqlServer:
				variant
					.WithReference(ResolveSqlServerDatabase(hostKit, configuration.EventStoreConnectionName))
					.WaitFor(ResolveSqlServerDatabase(hostKit, configuration.EventStoreConnectionName));
				break;
			case SampleEventStoreKind.Postgres:
				variant
					.WithReference(ResolvePostgresDatabase(hostKit, configuration.EventStoreConnectionName))
					.WaitFor(ResolvePostgresDatabase(hostKit, configuration.EventStoreConnectionName));
				break;
			case SampleEventStoreKind.MongoDB:
				AddMongoReference(variant, hostKit, configuration.EventStoreDatabaseName);
				mongoReferenced = true;
				break;
			case SampleEventStoreKind.AzureStorage:
				variant.WithReference(hostKit.AzureStorage.TableStorage).WaitFor(hostKit.AzureStorage.TableStorage);
				break;
		}

		switch (configuration.QueryStore)
		{
			case SampleQueryStoreKind.SqlServer:
				variant
					.WithReference(ResolveSqlServerDatabase(hostKit, configuration.QueryStoreConnectionName))
					.WaitFor(ResolveSqlServerDatabase(hostKit, configuration.QueryStoreConnectionName));
				break;
			case SampleQueryStoreKind.Postgres:
				variant
					.WithReference(ResolvePostgresDatabase(hostKit, configuration.QueryStoreConnectionName))
					.WaitFor(ResolvePostgresDatabase(hostKit, configuration.QueryStoreConnectionName));
				break;
			case SampleQueryStoreKind.MongoDB:
				if (!mongoReferenced)
					variant.WithReference(hostKit.MongoDB);

				variant.WaitFor(ResolveMongoDatabase(hostKit, configuration.QueryStoreDatabaseName));
				break;
		}
	}

	static void AddMongoReference(
		IResourceBuilder<ProjectResource> variant,
		SampleAppHostKit hostKit,
		string? databaseName
	)
	{
		variant.WithReference(hostKit.MongoDB);
		variant.WaitFor(ResolveMongoDatabase(hostKit, databaseName));
	}

	static IResourceBuilder<SqlServerDatabaseResource> ResolveSqlServerDatabase(
		SampleAppHostKit hostKit,
		string connectionName
	) =>
		connectionName switch
		{
			Platform.SqlDatabase => hostKit.SqlServer.Database,
			Platform.SqlSharedQueryDatabase => hostKit.SqlServer.SharedQueryDatabase,
			_ => throw new InvalidOperationException($"Unsupported SQL Server connection name '{connectionName}'."),
		};

	static IResourceBuilder<PostgresDatabaseResource> ResolvePostgresDatabase(
		SampleAppHostKit hostKit,
		string connectionName
	) =>
		connectionName switch
		{
			Platform.PostgresDatabase => hostKit.Postgres.Database,
			Platform.PostgresSharedQueryDatabase => hostKit.Postgres.SharedQueryDatabase,
			_ => throw new InvalidOperationException($"Unsupported Postgres connection name '{connectionName}'."),
		};

	static IResourceBuilder<MongoDBDatabaseResource> ResolveMongoDatabase(
		SampleAppHostKit hostKit,
		string? databaseName
	) =>
		databaseName switch
		{
			Platform.MongoDatabase => hostKit.MongoDB.Database,
			Platform.MongoSharedQueryDatabase => hostKit.MongoDB.SharedQueryDatabase,
			_ => throw new InvalidOperationException($"Unsupported MongoDB database name '{databaseName}'."),
		};
}
