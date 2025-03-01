using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB;

public record struct AggregateVersionData(int SavedVersion, int SnapshotVersion, int CurrentVersion)
{
	public static AggregateVersionData Create(IAggregate aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		return new()
		{
			SavedVersion = aggregate.Details.SavedVersion,
			SnapshotVersion = aggregate.Details.SnapshotVersion,
			CurrentVersion = aggregate.Details.CurrentVersion
		};
	}
}
