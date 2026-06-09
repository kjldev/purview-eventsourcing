using Purview.Telemetry;

namespace Purview.EventSourcing.MongoDB.Snapshots;

[Meter]
public interface IMongoDBSnapshotEventStoreTelemetry
{
	[Counter(AutoIncrement = true)]
	void SnapshotCreated(string aggregateType);
}
