using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.SqlServer.Events;
using Purview.EventSourcing.SqlServer.Snapshot;
using Purview.EventSourcing.SqlServer.Snapshots;

namespace Microsoft.Extensions.DependencyInjection;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddSqlServerEventStore()
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IEventStoreCore<>), typeof(SqlServerEventStore<>))
				.AddTransient(typeof(INonQueryableEventStore<>), typeof(SqlServerEventStore<>))
				.AddTransient(typeof(ISqlServerEventStore<>), typeof(SqlServerEventStore<>))
				.AddTransient<IEventStore, EventStoreFacade>()
				.TryAddSingleton<IEventStoreTransactionFactory, SqlServerEventStoreTransactionFactory>();

			services.AddSqlServerEventStoreTelemetry();

			services
				.AddOptions<SqlServerEventStoreOptions>()
				.Configure<IConfiguration>(
					(options, configuration) =>
					{
						configuration.GetSection(SqlServerEventStoreOptions.SqlServerEventStore).Bind(options);

						if (string.IsNullOrWhiteSpace(options.ConnectionString))
						{
							options.ConnectionString =
								configuration.GetConnectionString("eventstore-sqlserver")
								?? configuration.GetConnectionString("EventStore_SqlServer")
								?? configuration.GetConnectionString("SqlServer")
								// This will get picked up by the validation.
								?? default!;
						}
					}
				)
				.ValidateOnStart();

			return services;
		}

		public IServiceCollection AddSqlServerSnapshotQueryableEventStore(bool registerAsIEventStore = false)
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IQueryableEventStoreCore<>), typeof(SqlServerSnapshotEventStore<>))
				.AddTransient(typeof(ISqlServerSnapshotEventStore<>), typeof(SqlServerSnapshotEventStore<>))
				.AddSqlServerSnapshotEventStoreTelemetry();

			services.TryAddTransient<IQueryableEventStore, QueryableEventStoreFacade>();

			if (registerAsIEventStore)
			{
				services.AddTransient(typeof(IEventStoreCore<>), typeof(SqlServerSnapshotEventStore<>));
				services.TryAddTransient<IEventStore, EventStoreFacade>();
			}

			services
				.AddOptions<SqlServerSnapshotEventStoreOptions>()
				.Configure<IConfiguration>(
					(options, configuration) =>
					{
						configuration.GetSection(SqlServerSnapshotEventStoreOptions.SqlServerEventStore).Bind(options);

						if (string.IsNullOrWhiteSpace(options.ConnectionString))
						{
							options.ConnectionString ??=
								configuration.GetConnectionString("eventstore-sqlserver")
								?? configuration.GetConnectionString("EventStore_SqlServer")
								?? configuration.GetConnectionString("SqlServer")
								// This will get picked up by the validation.
								?? default!;
						}
					}
				)
				.ValidateOnStart();

			return services;
		}
	}
}
