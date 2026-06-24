using Purview.Telemetry;

namespace Purview.EventSourcing.MongoDB.Events;

[Logger]
public interface IMongoDBEventStoreTelemetry
{
	[Debug]
	void AggregateRetrievedFromCache(string aggregateId, string aggregateTypeFullName);

	[Debug]
	void GetAggregateStart(string aggregateId, string aggregateTypeFullName);

	[Error]
	void GetAggregateAtSpecificVersionFailed(
		string aggregateId,
		string aggregateTypeFullName,
		int specificVersion,
		Exception exception
	);

	[Debug]
	void ReconstitutedAggregateFromEvents(
		string aggregateId,
		string aggregateTypeFullName,
		string aggregateType,
		int eventCount,
		AggregateVersionData versionData
	);

	[Debug]
	void GetAggregateAtSpecificVersionStart(string aggregateId, int specificVersion, string aggregateTypeFullName);

	[Debug]
	void SaveContainedNoChanges(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Warning]
	void SkippedUnknownEvent(
		string aggregateId,
		string aggregateTypeFullName,
		string aggregateType,
		string eventType,
		int aggregateVersion
	);

	[Warning]
	void CannotApplyEvent(
		string aggregateId,
		string aggregateTypeFullName,
		string aggregateType,
		string eventType,
		string eventTypeFullName,
		int aggregateVersion
	);

	[Debug]
	void GetAggregateComplete(string aggregateId, string aggregateTypeFullName, long elapsedMilliseconds);

	[Error]
	void SnapshotDeserializationFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Warning]
	void CacheGetFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Warning]
	void CacheUpdateFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Debug]
	void WritingLargeEvent(string aggregateId, string blobName, long length, string fullName);

	[Debug]
	void AggregateDeleted(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Debug]
	void AggregateRestored(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Debug]
	void SaveCalled(string aggregateId, string aggregateTypeFullName, string aggregateType);

	[Error]
	void SaveFailedAtStorage(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Debug]
	void EventsAlreadyApplied(string aggregateId, string idempotencyId);

	[Error]
	void SaveFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Warning]
	void MissingEventType(string aggregateTypeFullName, string eventType);

	[Debug]
	void GetAggregateAtSpecificVersionComplete(
		string aggregateId,
		string aggregateTypeFullName,
		int specificVersion,
		long elapsedMilliseconds
	);

	[Warning]
	void SkippedMissingBlobEvent(string partitionKey, string rowKey, string serializedEventType, string blobName);

	[Warning]
	void MissingBlobEventType(
		string aggregateTypeFullName,
		string eventType,
		string serializedEventType,
		string blobEventTypeName
	);

	[Warning]
	void CacheRemovalFailure(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Warning]
	void EventDeserializationFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Debug]
	void SavedAggregate(string aggregateId, string aggregateTypeFullName, int eventCount, string aggregateType);

	[Error]
	void GetAggregateFailed(string aggregateId, string aggregateTypeFullName, Exception exception);

	[Warning]
	void StreamVersionExpectedToExistButNotFound(
		string aggregateId,
		string aggregateTypeName,
		string aggregateTypeFullName
	);

	[Debug]
	void StreamVersionNotFound(string aggregateId);

	[Debug]
	void StreamVersionFound(string aggregateId, int streamVersion, string aggregateType, bool isDeleted);

	[Error]
	void GetStreamVersionFailed(string aggregateId, Exception exception);

	[Debug]
	void GetStreamVersionStart(string aggregateId);

	[Debug]
	void GetStreamVersionComplete(string aggregateId, long elapsedMilliseconds);

	[Debug]
	void PermanentDeleteRequested(string aggregateId);

	[Error]
	void PermanentDeleteFailed(string aggregateId, Exception exception);

	[Debug]
	void PermanentDeleteComplete(string aggregateId);

	[Error]
	void GetIdempotencyMarkerFailed(string aggregateId, string idempotencyId, Exception exception);
}
