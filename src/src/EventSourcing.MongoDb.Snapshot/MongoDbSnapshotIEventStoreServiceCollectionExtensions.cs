using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.MongoDB.Snapshot;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class MongoDBSnapshotIEventStoreServiceCollectionExtensions
{
	public static IServiceCollection AddMongoDBSnapshotQueryableEventStore(
		this IServiceCollection services,
		bool registerAsIEventStore = false
	)
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
