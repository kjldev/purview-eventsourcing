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

	[Debug]
	[AutoCounter]
	[Event]
	void QueryCompleted(
		Activity? activity,
		[ExcludeTargets(Targets.Activities)] string aggregateType,
		int resultCount,
		[ExcludeTargets(Targets.Activities)] long elapsedMilliseconds
	);

	// Logging

	[Debug]
	void SnapshotSaveStart(string aggregateId, string aggregateType);

	[Debug]
	[AutoCounter]
	void SnapshotSaveComplete(string aggregateId, string aggregateType);

	[Error]
	void SnapshotSaveFailed(string aggregateId, string aggregateType, Exception exception);

	[Debug]
	void SnapshotDeleteStart(string aggregateId, string aggregateType);

	[Debug]
	[AutoCounter]
	void SnapshotDeleteComplete(string aggregateId, string aggregateType);

	[Error]
	void SnapshotDeleteFailed(string aggregateId, string aggregateType, Exception exception);

	[Debug]
	void SnapshotQueryStart(string aggregateType, int maxRecords);

	[Debug]
	[AutoCounter]
	void SnapshotQueryComplete(string aggregateType, int resultCount, long elapsedMilliseconds);

	[Error]
	void SnapshotQueryFailed(string aggregateType, Exception exception);
}
