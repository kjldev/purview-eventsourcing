using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.CosmosDb.Snapshot;
using Purview.EventSourcing.CosmosDb.Snapshots;

namespace Microsoft.Extensions.DependencyInjections;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddCosmosDbQueryableEventStore(bool registerAsIEventStore = false)
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IQueryableEventStoreCore<>), typeof(CosmosDbSnapshotEventStore<>))
				.AddTransient(typeof(ICosmosDbSnapshotEventStore<>), typeof(CosmosDbSnapshotEventStore<>));
			services.TryAddTransient<IQueryableEventStore, QueryableEventStoreFacade>();

			if (registerAsIEventStore)
			{
				services.AddTransient(typeof(IEventStoreCore<>), typeof(CosmosDbSnapshotEventStore<>));
				services.TryAddTransient<IEventStore, EventStoreFacade>();
			}

			services
				.AddOptions<CosmosDbEventStoreOptions>()
				.Configure<IConfiguration>(
					(options, configuration) =>
					{
						configuration.GetSection(CosmosDbEventStoreOptions.CosmosDbEventStore).Bind(options);

						options.ConnectionString ??=
							configuration.GetConnectionString("EventStore_CosmosDb")
							?? configuration.GetConnectionString("CosmosDb")
							// This will get picked up by the validation.
							?? default!;
					}
				)
				.ValidateOnStart();

			return services;
		}
	}
}
