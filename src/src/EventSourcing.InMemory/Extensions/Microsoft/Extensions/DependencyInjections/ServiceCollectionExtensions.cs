using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.InMemory.Events;
using Purview.EventSourcing.InMemory.Snapshots;
using Purview.EventSourcing.Internal;

namespace Microsoft.Extensions.DependencyInjections;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		public IServiceCollection AddInMemoryEventStore()
		{
			services.AddEventSourcing();

			services
				.AddTransient(typeof(IEventStoreCore<>), typeof(InMemoryEventStore<>))
				.AddTransient(typeof(INonQueryableEventStore<>), typeof(InMemoryEventStore<>))
				.AddTransient(typeof(IInMemoryEventStore<>), typeof(InMemoryEventStore<>))
				.AddTransient<IEventStore, EventStoreFacade>();

			return services;
		}

		public IServiceCollection AddInMemorySnapshotEventStore()
		{
			services.AddEventSourcing();

			services
				// Non-queryable
				.AddTransient(typeof(IEventStoreCore<>), typeof(InMemorySnapshotStore<>))
				.AddTransient(typeof(INonQueryableEventStore<>), typeof(InMemorySnapshotStore<>))
				.AddTransient(typeof(IInMemoryEventStore<>), typeof(InMemorySnapshotStore<>))
				// Queryable
				.AddTransient(typeof(IQueryableEventStoreCore<>), typeof(InMemorySnapshotStore<>))
				.AddTransient(typeof(IInMemorySnapshotStore<>), typeof(InMemorySnapshotStore<>));

			services
				.AddTransient<IQueryableEventStore, QueryableEventStoreFacade>()
				.AddTransient<IEventStore, EventStoreFacade>();

			return services;
		}
	}
}
