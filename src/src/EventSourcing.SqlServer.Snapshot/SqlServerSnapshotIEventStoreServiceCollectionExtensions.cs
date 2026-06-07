using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            .AddTransient(typeof(IQueryableEventStoreCore<>), typeof(SqlServerSnapshotEventStore<>))
            .AddTransient(
                typeof(ISqlServerSnapshotEventStore<>),
                typeof(SqlServerSnapshotEventStore<>)
            )
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
                    configuration
                        .GetSection(SqlServerSnapshotEventStoreOptions.SqlServerEventStore)
                        .Bind(options);

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
