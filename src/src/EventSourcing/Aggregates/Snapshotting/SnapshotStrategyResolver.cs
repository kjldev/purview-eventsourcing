namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// Resolves and evaluates snapshot strategies with the precedence:
/// operation context, selector, then default strategy.
/// </summary>
public static class SnapshotStrategyResolver
{
	public static ISnapshotStrategy<T> ResolveStrategy<T>(
		EventStoreOperationContext? context,
		ISnapshotStrategy<T> defaultStrategy,
		ISnapshotStrategySelector? selector = null
	)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(defaultStrategy);

		if (context?.TryGetSnapshotStrategy<T>(out var contextStrategy) == true)
			return contextStrategy!;

		var selectedStrategy = context?.SnapshotStrategySelector?.Resolve<T>() ?? selector?.Resolve<T>();

		return selectedStrategy ?? defaultStrategy;
	}

	public static bool ShouldSnapshot<T>(
		T aggregate,
		int eventsApplied,
		EventStoreOperationContext? context,
		ISnapshotStrategy<T> defaultStrategy,
		ISnapshotStrategySelector? selector = null
	)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		var strategy = ResolveStrategy(context, defaultStrategy, selector);
		var savedVersion = aggregate.Details.SavedVersion;
		try
		{
			aggregate.Details.SavedVersion = aggregate.Details.CurrentVersion;
			return strategy.ShouldSnapshot(aggregate, eventsApplied);
		}
		finally
		{
			aggregate.Details.SavedVersion = savedVersion;
		}
	}
}
