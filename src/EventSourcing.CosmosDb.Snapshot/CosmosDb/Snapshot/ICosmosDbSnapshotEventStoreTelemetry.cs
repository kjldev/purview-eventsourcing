using Purview.Telemetry.Metrics;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

[Meter]
public interface ICosmosDbSnapshotEventStoreTelemetry
{
	[Counter(AutoIncrement = true)]
	void SnapshotCreated(string aggregateType);
}
