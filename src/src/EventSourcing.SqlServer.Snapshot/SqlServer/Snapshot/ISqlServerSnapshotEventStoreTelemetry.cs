using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Purview.Telemetry;

namespace Purview.EventSourcing.SqlServer.Snapshot;

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

    [Log(LogLevel.Debug)]
    void SnapshotSaveStart(string aggregateId, string aggregateType);

    [Log(LogLevel.Debug)]
    void SnapshotSaveComplete(string aggregateId, string aggregateType);

    [Log(LogLevel.Error)]
    void SnapshotSaveFailed(string aggregateId, string aggregateType, Exception exception);

    [Log(LogLevel.Debug)]
    void SnapshotDeleteStart(string aggregateId, string aggregateType);

    [Log(LogLevel.Debug)]
    void SnapshotDeleteComplete(string aggregateId, string aggregateType);

    [Log(LogLevel.Error)]
    void SnapshotDeleteFailed(string aggregateId, string aggregateType, Exception exception);

    [Log(LogLevel.Debug)]
    void SnapshotQueryStart(string aggregateType, int maxRecords);

    [Log(LogLevel.Debug)]
    void SnapshotQueryComplete(string aggregateType, int resultCount, long elapsedMilliseconds);

    [Log(LogLevel.Error)]
    void SnapshotQueryFailed(string aggregateType, Exception exception);
}
