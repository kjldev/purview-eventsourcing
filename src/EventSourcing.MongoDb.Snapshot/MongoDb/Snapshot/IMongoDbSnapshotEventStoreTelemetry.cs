using Purview.Telemetry.Metrics;

namespace Purview.EventSourcing.MongoDB.Snapshot;

[Meter]
public interface IMongoDBSnapshotEventStoreTelemetry
{
	[Counter(AutoIncrement = true)]
	void SnapshotCreated(string aggregateType);
}
