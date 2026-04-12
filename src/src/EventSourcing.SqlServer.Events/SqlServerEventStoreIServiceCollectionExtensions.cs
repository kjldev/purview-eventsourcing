using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.SqlServer;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlServerEventStoreIServiceCollectionExtensions
{
	public static IServiceCollection AddSqlServerEventStore([NotNull] this IServiceCollection services)
	{
		services.AddEventSourcing();

		services
			.AddTransient(typeof(IEventStore<>), typeof(SqlServerEventStore<>))
			.AddTransient(typeof(INonQueryableEventStore<>), typeof(SqlServerEventStore<>))
			.AddTransient(typeof(ISqlServerEventStore<>), typeof(SqlServerEventStore<>))
			.AddSqlServerEventStoreTelemetry();

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
}
