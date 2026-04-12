using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlServerSnapshotIEventStoreServiceCollectionExtensions
{
	public static IServiceCollection AddSqlServerSnapshotQueryableEventStore(
		this IServiceCollection services,
		bool registerAsIEventStore = false
	)
	{
		services.AddEventSourcing();

		services
			.AddTransient(typeof(IQueryableEventStore<>), typeof(SqlServerSnapshotEventStore<>))
			.AddTransient(typeof(ISqlServerSnapshotEventStore<>), typeof(SqlServerSnapshotEventStore<>))
			.AddSqlServerSnapshotEventStoreTelemetry();

		if (registerAsIEventStore)
			services.AddTransient(typeof(IEventStore<>), typeof(SqlServerSnapshotEventStore<>));

		services
			.AddOptions<SqlServerSnapshotEventStoreOptions>()
			.Configure<IConfiguration>(
				(options, configuration) =>
				{
					configuration.GetSection(SqlServerSnapshotEventStoreOptions.SqlServerEventStore).Bind(options);

					options.ConnectionString ??=
						configuration.GetConnectionString("eventstore-sqlserver")
						?? configuration.GetConnectionString("EventStore_SqlServer")
						?? configuration.GetConnectionString("SqlServer")
						// This will get picked up by the validation.
						?? default!;
				}
			)
			.ValidateOnStart();

		return services;
	}
}
