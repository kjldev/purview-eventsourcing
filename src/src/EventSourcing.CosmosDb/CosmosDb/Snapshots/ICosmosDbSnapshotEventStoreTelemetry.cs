using Purview.Telemetry;

namespace Purview.EventSourcing.CosmosDb.Snapshots;

[Meter]
public interface ICosmosDbSnapshotEventStoreTelemetry
{
	[Counter(AutoIncrement = true)]
	void SnapshotCreated(string aggregateType);
}
