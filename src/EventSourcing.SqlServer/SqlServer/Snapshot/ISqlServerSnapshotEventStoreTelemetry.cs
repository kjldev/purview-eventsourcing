using Purview.Telemetry;

namespace Purview.EventSourcing.SqlServer.Snapshot;

[Meter]
public interface ISqlServerSnapshotEventStoreTelemetry
{
	[Counter(AutoIncrement = true)]
	void SnapshotCreated(string aggregateType);
}
