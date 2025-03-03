using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.MongoDB;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class MongoDBEventStoreIServiceCollectionExtensions
{
	public static IServiceCollection AddMongoDBEventStore([NotNull] this IServiceCollection services)
	{
		services.AddEventSourcing();

		services
			.AddTransient(typeof(IEventStore<>), typeof(MongoDBEventStore<>))
			.AddTransient(typeof(INonQueryableEventStore<>), typeof(MongoDBEventStore<>))
			.AddTransient(typeof(IMongoDBEventStore<>), typeof(MongoDBEventStore<>))
			.AddMongoDBEventStoreTelemetry();

		services.AddMongoDBClientTelemetry();

		services
			.AddOptions<MongoDBEventStoreOptions>()
			.Configure<IConfiguration>((options, configuration) =>
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
			})
			.ValidateOnStart();

		return services;
	}
}
