using Microsoft.Extensions.Logging;
using Purview.Telemetry.Logging;

namespace Purview.EventSourcing.MongoDB;

[Logger]
public interface IMongoDBEventStoreTelemetry
{
	[Log(LogLevel.Debug)]
	void AggregateRetrievedFromCache(string aggregateId, string aggregateTypeFullName);

	[Log(LogLevel.Debug)]
	void GetAggregateStart(string aggregateId, string aggregateTypeFullName);

	[Log(LogLevel.Error)]
	void GetAggregateAtSpecificVersionFailed(string aggregateId, string aggregateTypeFullName, int specificVersion, Exception exception);

	[Log(LogLevel.Debug)]
	void ReconstitutedAggregateFromEvents(string aggregateId, string aggregateTypeFullName, string aggregateType, int eventCount, AggregateVersionData versionData);

	[Log(LogLevel.Debug)]
	void GetAggregateAtSpecificVersionStart(string aggregateId, int specificVersion, string aggregateTypeFullName);

	[Log(LogLevel.Debug)]
	void SaveContainedNoChanges(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Log(LogLevel.Warning)]
	void SkippedUnknownEvent(string aggregateId, string aggregateTypeFullName, string aggregateType, string eventType, int aggregateVersion);

	[Log(LogLevel.Warning)]
	void CannotApplyEvent(string aggregateId, string aggregateTypeFullName, string aggregateType, string eventType, string eventTypeFullName, int aggregateVersion);

	[Log(LogLevel.Debug)]
	void GetAggregateComplete(string aggregateId, string aggregateTypeFullName, long elapsedMilliseconds);

	[Log(LogLevel.Error)]
	void SnapshotDeserializationFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Warning)]
	void CacheGetFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Warning)]
	void CacheUpdateFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Debug)]
	void WritingLargeEvent(string aggregateId, string blobName, long length, string fullName);

	[Log(LogLevel.Debug)]
	void AggregateDeleted(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Log(LogLevel.Debug)]
	void AggregateRestored(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Log(LogLevel.Debug)]
	void SaveCalled(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Log(LogLevel.Error)]
	void SaveFailedAtStorage(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Debug)]
	void EventsAlreadyApplied(string aggregateId, string idempotencyId);

	[Log(LogLevel.Error)]
	void SaveFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Warning)]
	void MissingEventType(string aggregateTypeFullName, string eventType);

	[Log(LogLevel.Debug)]
	void GetAggregateAtSpecificVersionComplete(string aggregateId, string aggregateTypeFullName, int specificVersion, long elapsedMilliseconds);

	[Log(LogLevel.Warning)]
	void SkippedMissingBlobEvent(string partitionKey, string rowKey, string serializedEventType, string blobName);

	[Log(LogLevel.Warning)]
	void MissingBlobEventType(string aggregateTypeFullName, string eventType, string serializedEventType, string blobEventTypeName);

	[Log(LogLevel.Warning)]
	void CacheRemovalFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Warning)]
	void EventDeserializationFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Debug)]
	void SavedAggregate(string aggregateId, string aggregateTypeFullName, int eventCount, string aggregateType);

	[Log(LogLevel.Error)]
	void GetAggregateFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Log(LogLevel.Warning)]
	void StreamVersionExpectedToExistButNotFound(string aggregateId);

	[Log(LogLevel.Debug)]
	void StreamVersionNotFound(string aggregateId);

	[Log(LogLevel.Debug)]
	void StreamVersionFound(string aggregateId, int streamVersion, string aggregateType, bool isDeleted);

	[Log(LogLevel.Error)]
	void GetStreamVersionFailed(string aggregateId, Exception exception);

	[Log(LogLevel.Debug)]
	void GetStreamVersionStart(string aggregateId);

	[Log(LogLevel.Debug)]
	void GetStreamVersionComplete(string aggregateId, long elapsedMilliseconds);

	[Log(LogLevel.Debug)]
	void PermanentDeleteRequested(string aggregateId);

	[Log(LogLevel.Critical)]
	void PermanentDeleteFailed(string aggregateId, Exception exception);

	[Log(LogLevel.Debug)]
	void PermanentDeleteComplete(string aggregateId);

	[Log(LogLevel.Error)]
	void GetIdempotencyMarkerFailed(string aggregateId, string idempotencyId, Exception exception);
}
