using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.MongoDB.Snapshot;

namespace Purview;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class MongoDBSnapshotIEventStoreServiceCollectionExtensions
{
	public static IServiceCollection AddMongoDBSnapshotQueryableEventStore(this IServiceCollection services, bool registerAsIEventStore = false)
	{
		services.AddEventSourcing();

		services
			.AddTransient(typeof(IQueryableEventStore<>), typeof(MongoDBSnapshotEventStore<>))
			.AddTransient(typeof(IMongoDBSnapshotEventStore<>), typeof(MongoDBSnapshotEventStore<>))
			.AddMongoDBSnapshotEventStoreTelemetry();

		if (registerAsIEventStore)
			services.AddTransient(typeof(IEventStore<>), typeof(MongoDBSnapshotEventStore<>));

		services
			.AddOptions<MongoDBEventStoreOptions>()
			.Configure<IConfiguration>((options, configuration) =>
			{
				configuration.GetSection(MongoDBEventStoreOptions.MongoDBEventStore).Bind(options);

				options.ConnectionString ??=
					configuration.GetConnectionString("EventStore_MongoDBSnapshot")
						?? configuration.GetConnectionString("MongoDBSnapshot")
						?? configuration.GetConnectionString("EventStore_MongoDB")
						?? configuration.GetConnectionString("MongoDB")
						// This will get picked up by the validation.
						?? default!;
			})
			.ValidateOnStart();

		return services;
	}
}
