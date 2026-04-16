namespace Purview.EventSourcing.EventsOnly.MongoDb;

public interface IMongoDBEventStoreTests
{
	Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(CancellationToken cancellationToken);

	Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(CancellationToken cancellationToken);

	Task DeleteAsync_GivenDelete_NotifiesChangeFeed(CancellationToken cancellationToken);

	Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(CancellationToken cancellationToken);

	Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(
		CancellationToken cancellationToken
	);

	Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
		int aggregateCount,
		CancellationToken cancellationToken
	);

	Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	);

	Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	);

	Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
		CancellationToken cancellationToken
	);

	Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	);

	Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	);

	Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	);

	Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	);

	Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		int previousEventsToCreate,
		CancellationToken cancellationToken
	);

	Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(CancellationToken cancellationToken);

	Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		CancellationToken cancellationToken
	);

	Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		int expectedEventCount,
		CancellationToken cancellationToken
	);

	Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(CancellationToken cancellationToken);

	Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(CancellationToken cancellationToken);

	Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(CancellationToken cancellationToken);

	Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(CancellationToken cancellationToken);

	Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(
		int eventsToCreate,
		CancellationToken cancellationToken
	);

	Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(
		CancellationToken cancellationToken
	);

	Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(CancellationToken cancellationToken);

	Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(CancellationToken cancellationToken);

	Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(CancellationToken cancellationToken);

	Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(
		CancellationToken cancellationToken
	);

	Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(
		CancellationToken cancellationToken
	);

	Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(CancellationToken cancellationToken);

	Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(
		int eventsToGenerate,
		CancellationToken _
	);

	Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
		int eventsToGenerate,
		CancellationToken cancellationToken
	);
}
