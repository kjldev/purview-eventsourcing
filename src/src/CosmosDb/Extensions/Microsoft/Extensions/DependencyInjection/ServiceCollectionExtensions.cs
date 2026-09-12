using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.CosmosDb.Snapshot;
using Purview.EventSourcing.CosmosDb.Snapshots;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection extension methods for registering the Azure Cosmos DB snapshot event store.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers the Cosmos DB snapshot event store as the queryable event store.
		/// </summary>
		/// <param name="registerAsIEventStore">When <see langword="true"/>, the store is also registered as the default <see cref="IEventStore"/>; otherwise only the queryable registration is added.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining further registrations.</returns>
		/// <remarks>
		/// The store is registered as <see cref="IQueryableEventStoreCore{T}"/> and
		/// <see cref="ICosmosDbSnapshotEventStore{T}"/>, and options are
		/// bound from the <see cref="CosmosDbEventStoreOptions.CosmosDbEventStore"/>
		/// configuration section, falling back to the <c>"EventStore_CosmosDb"</c> or <c>"CosmosDb"</c> connection string.
		/// </remarks>
		public IServiceCollection AddCosmosDbSnapshotQueryableEventStore(bool registerAsIEventStore = false)
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

			services.AddEventStoreCapabilities(
				new EventStoreCapabilities(
					EventStoreTransactionGuarantee.BestEffort,
					SupportsEventStreams: false,
					SupportsSnapshots: true,
					SnapshotSchemaVersioning: SnapshotSchemaSupport.SingleVersion,
					PreservedMetadata: PreservedEventMetadata.None,
					SupportsQueries: true,
					SupportsIdempotencyMarkers: false,
					Concurrency: ConcurrencyGuarantee.Optimistic,
					OperationalLimitations: [EventStoreOperationalLimitation.NoEventStream]
				)
			);

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
