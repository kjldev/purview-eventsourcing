namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// Snapshot strategy selector that supports a default strategy and per-aggregate overrides.
/// </summary>
public sealed class SnapshotStrategySelector : ISnapshotStrategySelector
{
	readonly Dictionary<Type, object> _snapshotStrategies = [];
	object? _defaultStrategy;

	/// <summary>
	/// Sets the default snapshot strategy used when no per-aggregate override exists.
	/// </summary>
	public SnapshotStrategySelector SetDefault<T>(ISnapshotStrategy<T> strategy)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(strategy);
		_defaultStrategy = strategy;
		return this;
	}

	/// <summary>
	/// Sets the snapshot strategy for <typeparamref name="T"/>.
	/// </summary>
	public SnapshotStrategySelector Set<T>(ISnapshotStrategy<T> strategy)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(strategy);
		_snapshotStrategies[typeof(T)] = strategy;
		return this;
	}

	/// <inheritdoc />
	public ISnapshotStrategy<T>? Resolve<T>()
		where T : class, IAggregate, new()
	{
		if (_snapshotStrategies.TryGetValue(typeof(T), out var strategy))
			return strategy as ISnapshotStrategy<T>;

		return _defaultStrategy as ISnapshotStrategy<T>;
	}
}
