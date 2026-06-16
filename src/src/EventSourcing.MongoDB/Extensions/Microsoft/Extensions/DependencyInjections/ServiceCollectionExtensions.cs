using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.MongoDB;
using Purview.EventSourcing.MongoDB.Events;
using Purview.EventSourcing.MongoDB.Snapshots;

namespace Microsoft.Extensions.DependencyInjections;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension([NotNull] IServiceCollection services)
	{
		public IServiceCollection AddMongoDBEventStore()
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IEventStoreCore<>), typeof(MongoDBEventStore<>))
				.AddTransient(typeof(INonQueryableEventStore<>), typeof(MongoDBEventStore<>))
				.AddTransient(typeof(IMongoDBEventStore<>), typeof(MongoDBEventStore<>))
				.AddTransient<IEventStore, EventStoreFacade>()
				.AddMongoDBEventStoreTelemetry();

			services.AddMongoDBClientTelemetry();

			services
				.AddOptions<MongoDBEventStoreOptions>()
				.Configure<IConfiguration>(
					(options, configuration) =>
					{
						configuration.GetSection(MongoDBEventStoreOptions.MongoDBEventStore).Bind(options);

						if (string.IsNullOrWhiteSpace(options.ConnectionString))
						{
							options.ConnectionString =
								configuration.GetConnectionString("EventStore_MongoDB")
								?? configuration.GetConnectionString("MongoDB")
								// This will get picked up by the validation.
								?? default!;
						}
					}
				)
				.ValidateOnStart();

			return services;
		}

		public IServiceCollection AddMongoDBSnapshotQueryableEventStore(bool registerAsIEventStore = false)
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IQueryableEventStoreCore<>), typeof(MongoDBSnapshotEventStore<>))
				.AddTransient(typeof(IMongoDBSnapshotEventStore<>), typeof(MongoDBSnapshotEventStore<>))
				.AddMongoDBSnapshotEventStoreTelemetry();

			services.TryAddTransient<IQueryableEventStore, QueryableEventStoreFacade>();

			if (registerAsIEventStore)
			{
				services.AddTransient(typeof(IEventStoreCore<>), typeof(MongoDBSnapshotEventStore<>));
				services.TryAddTransient<IEventStore, EventStoreFacade>();
			}

			services
				.AddOptions<MongoDBEventStoreOptions>()
				.Configure<IConfiguration>(
					(options, configuration) =>
					{
						configuration.GetSection(MongoDBEventStoreOptions.MongoDBEventStore).Bind(options);

						options.ConnectionString ??=
							configuration.GetConnectionString("EventStore_MongoDBSnapshot")
							?? configuration.GetConnectionString("MongoDBSnapshot")
							?? configuration.GetConnectionString("EventStore_MongoDB")
							?? configuration.GetConnectionString("MongoDB")
							// This will get picked up by the validation.
							?? default!;
					}
				)
				.ValidateOnStart();

			return services;
		}
	}
}
