using System.Diagnostics.CodeAnalysis;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Purview.EventSourcing.AzureStorage;
using Purview.EventSourcing.MongoDB.Events;
using Purview.EventSourcing.MongoDB.Snapshots;
using Purview.EventSourcing.Samples;
using Purview.EventSourcing.Samples.Options;
using Purview.EventSourcing.Samples.Web.Services;

namespace Microsoft.Extensions.Hosting;

static class HostApplicationBuilderExtensions
{
	extension<TBuilder>(TBuilder builder)
		where TBuilder : IHostApplicationBuilder
	{
		public TBuilder AddSampleEventStore()
		{
			var sampleStoreOptions =
				builder.Configuration.GetSection(SampleStoreOptions.SectionName).Get<SampleStoreOptions>() ?? new();
			builder.Services.AddSingleton(sampleStoreOptions);
			builder
				.Services.AddOptions<SampleStoreOptions>()
				.BindConfiguration(SampleStoreOptions.SectionName)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			builder
				.ConfigureStoreOptions(sampleStoreOptions)
				.RegisterEventStore(sampleStoreOptions)
				.RegisterQueryStore(sampleStoreOptions)
				.RegisterAdmin(sampleStoreOptions);

			// Use Redis when available (e.g. via Aspire AppHost); fall back to in-memory for standalone dev runs
			if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(Platform.Redis)))
				builder.Services.AddDistributedMemoryCache();
			else
				builder.AddRedisDistributedCache(Platform.Redis);

			if (sampleStoreOptions.AdminAPIAvailable)
			{
				builder.Services.AddPurviewEventSourcingAdminSite(
					enableRazorRuntimeCompilation: builder.Environment.IsDevelopment()
				);
			}

			return builder;
		}

		public TBuilder AddSampleServices()
		{
			builder.Services.AddDomainServices();
			builder.Services.AddScoped<IAggregateAuditService, AggregateAuditService>();
			builder.Services.AddScoped<SampleSeedService>().AddSampleSeedServiceTelemetry();

			// Register product image service — uses Azure Blob Storage when configured, no-op otherwise
			var blobConnectionString = AzureStorageConnectionStringComposer.Normalize(
				builder.Configuration.GetConnectionString(Platform.AzureStorageBlob)
			);
			builder.Services.AddSingleton<IProductImageService>(serviceProvider =>
			{
				if (string.IsNullOrWhiteSpace(blobConnectionString))
					return new NullProductImageService();

				try
				{
					return new ProductImageService(new BlobServiceClient(blobConnectionString));
				}
				catch (FormatException ex)
				{
					serviceProvider
						.GetRequiredService<ILoggerFactory>()
						.CreateLogger("ProductImageService")
						.LogWarning(ex, "Invalid Azure Blob connection string; product images are disabled.");
					return new NullProductImageService();
				}
				catch (ArgumentException ex)
				{
					serviceProvider
						.GetRequiredService<ILoggerFactory>()
						.CreateLogger("ProductImageService")
						.LogWarning(ex, "Invalid Azure Blob connection string; product images are disabled.");
					return new NullProductImageService();
				}
			});

			return builder;
		}

		TBuilder ConfigureStoreOptions([NotNull] SampleStoreOptions sampleStoreOptions)
		{
#pragma warning disable IDE0010 // Add missing cases
			switch (sampleStoreOptions.EventStore)
			{
				case SampleEventStoreKind.MongoDB:
					builder
						.Services.AddOptions<MongoDBEventStoreOptions>()
						.Configure(options =>
							options.Database = sampleStoreOptions.EventStoreDatabaseName ?? Platform.MongoDatabase
						);
					break;
				case SampleEventStoreKind.AzureStorage:
					builder
						.Services.AddOptions<AzureStorageEventStoreOptions>()
						.Configure(options =>
						{
							options.Table = $"EventStore{NormalizeAlphaNumeric(sampleStoreOptions.CurrentKey)}";
							options.Container = $"eventstore-{NormalizeKebab(sampleStoreOptions.CurrentKey)}";
						});
					builder.Services.PostConfigure<AzureStorageEventStoreOptions>(options =>
						options.ConnectionString = AzureStorageConnectionStringComposer.BuildEventStoreConnectionString(
							builder.Configuration.GetConnectionString(sampleStoreOptions.EventStoreConnectionName),
							builder.Configuration.GetConnectionString(Platform.AzureStorageBlob),
							options.ConnectionString
						)
					);
					break;
			}
#pragma warning restore IDE0010 // Add missing cases

			if (sampleStoreOptions.QueryStore == SampleQueryStoreKind.MongoDB)
			{
				builder
					.Services.AddOptions<MongoDBSnapshotEventStoreOptions>()
					.Configure(options =>
						options.Database = sampleStoreOptions.QueryStoreDatabaseName ?? Platform.MongoDatabase
					);
			}

			return builder;
		}

		TBuilder RegisterEventStore([NotNull] SampleStoreOptions sampleStoreOptions)
		{
			switch (sampleStoreOptions.EventStore)
			{
				case SampleEventStoreKind.SqlServer:
					builder.Services.AddSqlServerEventStore(sampleStoreOptions.EventStoreConnectionName);
					break;
				case SampleEventStoreKind.Postgres:
					builder.Services.AddPostgresEventStore(sampleStoreOptions.EventStoreConnectionName);
					break;
				case SampleEventStoreKind.MongoDB:
					builder.Services.AddMongoDBEventStore(sampleStoreOptions.EventStoreConnectionName);
					break;
				case SampleEventStoreKind.AzureStorage:
					builder.Services.AddAzureStorageEventStore(sampleStoreOptions.EventStoreConnectionName);
					break;
				default:
					throw new InvalidOperationException($"Unsupported event store '{sampleStoreOptions.EventStore}'.");
			}

			return builder;
		}

		TBuilder RegisterQueryStore([NotNull] SampleStoreOptions sampleStoreOptions)
		{
			switch (sampleStoreOptions.QueryStore)
			{
				case SampleQueryStoreKind.SqlServer:
					builder.Services.AddSqlServerSnapshotQueryableEventStore(
						sampleStoreOptions.QueryStoreConnectionName
					);
					break;
				case SampleQueryStoreKind.Postgres:
					builder.Services.AddPostgresSnapshotQueryableEventStore(
						sampleStoreOptions.QueryStoreConnectionName
					);
					break;
				case SampleQueryStoreKind.MongoDB:
					builder.Services.AddMongoDBSnapshotQueryableEventStore(sampleStoreOptions.QueryStoreConnectionName);
					break;
				default:
					throw new InvalidOperationException($"Unsupported query store '{sampleStoreOptions.QueryStore}'.");
			}

			return builder;
		}

		TBuilder RegisterAdmin([NotNull] SampleStoreOptions sampleStoreOptions)
		{
			if (!sampleStoreOptions.AdminAPIAvailable)
				return builder;

			builder
				.Services.AddAuthentication(SampleAdminAuthenticationHandler.SchemeName)
				.AddScheme<AuthenticationSchemeOptions, SampleAdminAuthenticationHandler>(
					SampleAdminAuthenticationHandler.SchemeName,
					configureOptions: null
				);
			builder.Services.AddAuthorizationBuilder().AddPurviewEventSourcingAdminPolicies();
			builder.Services.AddPurviewEventSourcingAdminSecurity(new SampleAdminPermissionProvider());
			builder.AddPurviewEventSourcingAdminAPI(options =>
			{
				options.RoutePrefix = sampleStoreOptions.AdminAPIPath;
				options.Features.ExportEvents = true;
			});
			builder.AddPurviewEventSourcingAdminOpenAPI();

#pragma warning disable IDE0010 // Add missing cases
			switch (sampleStoreOptions.AdminStore)
			{
				case SampleAdminStoreKind.SqlServer:
					builder.AddPurviewEventSourcingAdminSqlServer();
					break;
				case SampleAdminStoreKind.Postgres:
					builder.AddPurviewEventSourcingAdminPostgres();
					break;
				case SampleAdminStoreKind.MongoDB:
					builder.Services.AddPurviewEventSourcingAdminMongoDB(
						sampleStoreOptions.AdminDatabaseName
							?? sampleStoreOptions.EventStoreDatabaseName
							?? sampleStoreOptions.QueryStoreDatabaseName
							?? Platform.MongoDatabase
					);
					break;
				case SampleAdminStoreKind.AzureStorage:
					builder.AddPurviewEventSourcingAdminAzureStorage();
					break;
				default:
					throw new InvalidOperationException($"Unsupported admin store '{sampleStoreOptions.AdminStore}'.");
			}

			return builder;
		}
	}

	static string NormalizeAlphaNumeric(string value) => new([.. value.Where(char.IsLetterOrDigit)]);

	[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
	static string NormalizeKebab(string value)
	{
		string normalized = new([
			.. value.ToLowerInvariant().Where(character => char.IsLetterOrDigit(character) || character == '-'),
		]);

		return string.IsNullOrWhiteSpace(normalized) ? "sample" : normalized;
	}
}
