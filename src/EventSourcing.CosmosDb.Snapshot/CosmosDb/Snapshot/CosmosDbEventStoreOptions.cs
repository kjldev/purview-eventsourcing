using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

public sealed class CosmosDbEventStoreOptions : CosmosDbOptions
{
	public const string CosmosDbEventStore = "EventStore:CosmosDbSnapshot";

	public CosmosDbEventStoreOptions()
	{
		PartitionKeyPath = $"/{nameof(IAggregate.AggregateType)}";
	}
}
