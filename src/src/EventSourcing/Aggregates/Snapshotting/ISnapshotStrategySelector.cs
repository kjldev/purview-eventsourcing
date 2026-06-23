namespace Purview.EventSourcing.Aggregates.Snapshotting;

/// <summary>
/// Selects a snapshot strategy for a given aggregate type.
/// </summary>
public interface ISnapshotStrategySelector
{
	/// <summary>
	/// Resolves the snapshot strategy for <typeparamref name="T"/>.
	/// </summary>
	ISnapshotStrategy<T>? Resolve<T>()
		where T : class, IAggregate, new();
}
