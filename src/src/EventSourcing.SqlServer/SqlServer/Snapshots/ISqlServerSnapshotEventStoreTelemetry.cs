using System.Diagnostics;
using Purview.Telemetry;

namespace Purview.EventSourcing.SqlServer.Snapshots;

[ActivitySource]
[Logger]
[Meter]
public interface ISqlServerSnapshotEventStoreTelemetry
{
	// Activities (distributed tracing)

	[Activity]
	Activity? SnapshotSave(string aggregateId, [Baggage] string aggregateType);

	[Activity]
	Activity? SnapshotQuery([Baggage] string aggregateType);

	[Activity]
	Activity? SnapshotDelete(string aggregateId, [Baggage] string aggregateType);

	[Event]
	void QueryCompleted(Activity? activity, int resultCount);

	// Metrics (counters)

	[AutoCounter]
	void SnapshotCreated(string aggregateType);

	[AutoCounter]
	void SnapshotDeleted(string aggregateType);

	[AutoCounter]
	void SnapshotQueried(string aggregateType);

	// Logging

	[Debug]
	void SnapshotSaveStart(string aggregateId, string aggregateType);

	[Debug]
	void SnapshotSaveComplete(string aggregateId, string aggregateType);

	[Error]
	void SnapshotSaveFailed(string aggregateId, string aggregateType, Exception exception);

	[Debug]
	void SnapshotDeleteStart(string aggregateId, string aggregateType);

	[Debug]
	void SnapshotDeleteComplete(string aggregateId, string aggregateType);

	[Error]
	void SnapshotDeleteFailed(string aggregateId, string aggregateType, Exception exception);

	[Debug]
	void SnapshotQueryStart(string aggregateType, int maxRecords);

	[Debug]
	void SnapshotQueryComplete(string aggregateType, int resultCount, long elapsedMilliseconds);

	[Error]
	void SnapshotQueryFailed(string aggregateType, Exception exception);
}
