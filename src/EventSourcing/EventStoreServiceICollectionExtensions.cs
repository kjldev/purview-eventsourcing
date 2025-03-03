using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
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
			.AddScoped(typeof(IAggregateChangeFeedNotifier<>), typeof(AggregateChangeFeedNotifier<>));

		return services;
	}

	public static IServiceCollection AddNullQueryableEventStore(this IServiceCollection services)
	{
		services
			.AddTransient(typeof(IQueryableEventStore<>), typeof(NullQueryableEventStore<>));

		return services;
	}
}
