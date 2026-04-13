using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Aggregates.Events.Upcasting;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.EventStores.NullQueryable;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerStepThrough]
public static class EventStoreServiceICollectionExtensions
{
	/// <summary>
	/// Register the event store components.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> where the components should be registered.</param>
	/// <returns>The <paramref name="services"/> passed in.</returns>
	public static IServiceCollection AddEventSourcing(this IServiceCollection services)
	{
		services
			.AddSingleton<IAggregateEventNameMapper, AggregateEventNameMapper>()
			.AddAggregateChangeFeedNotifierTelemetry()
			.AddScoped<IAggregateRequirementsManager, AggregateRequiredServiceManager>()
			.AddScoped(typeof(IAggregateChangeFeedNotifier<>), typeof(AggregateChangeFeedNotifier<>))
			.AddSingleton<IEventUpcasterRegistry, EventUpcasterRegistry>();

		services.TryAddSingleton<IEventStoreTransactionFactory, EventStoreTransactionFactory>();

		return services;
	}

	public static IServiceCollection AddNullQueryableEventStore(this IServiceCollection services)
	{
		services.AddTransient(typeof(IQueryableEventStore<>), typeof(NullQueryableEventStore<>));

		return services;
	}

	/// <summary>
	/// Registers an <see cref="IEventUpcaster{TSource,TTarget}"/> so that legacy events stored
	/// under <typeparamref name="TSource"/> are automatically up-cast to
	/// <typeparamref name="TTarget"/> during event replay.
	/// </summary>
	/// <typeparam name="TSource">The legacy event type.</typeparam>
	/// <typeparam name="TTarget">The current event type.</typeparam>
	/// <typeparam name="TUpcaster">The upcaster implementation.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/>.</param>
	/// <returns>The <paramref name="services"/> for fluent chaining.</returns>
	/// <remarks>
	/// This method also registers a corresponding <see cref="IEventUpcasterDescriptor"/> so that
	/// the <see cref="IEventUpcasterRegistry"/> can discover the upcaster at runtime.
	/// <see cref="AddEventSourcing"/> must be called before (or after) this method on the same
	/// <paramref name="services"/> collection.
	/// </remarks>
	public static IServiceCollection AddEventUpcaster<TSource, TTarget, TUpcaster>(
		this IServiceCollection services
	)
		where TSource : IEvent
		where TTarget : IEvent
		where TUpcaster : class, IEventUpcaster<TSource, TTarget>
	{
		services
			.AddSingleton<IEventUpcaster<TSource, TTarget>, TUpcaster>()
			.AddSingleton<IEventUpcasterDescriptor>(
				sp => new EventUpcasterDescriptor<TSource, TTarget>(
					sp.GetRequiredService<IEventUpcaster<TSource, TTarget>>()
				)
			);

		return services;
	}
}
