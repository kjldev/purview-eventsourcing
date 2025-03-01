namespace Purview.EventSourcing.MongoDB;

[Collection("MongoDB")]
[NCrunch.Framework.Category("MongoDB")]
[NCrunch.Framework.Category("Storage")]
public sealed partial class MongoDBEventStoreTests(MongoDBEventStoreFixture fixture) : IClassFixture<MongoDBEventStoreFixture>
{
	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenDelete_NotifiesChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(Type aggregateType, int aggregateCount)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(aggregateCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException();
	}

	[Theory]
	[MemberData(nameof(SnapshotEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(Type aggregateType, int eventsToCreate, int numberOfOldEventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(eventsToCreate, numberOfOldEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(Type aggregateType, int eventsToCreate, int numberOfOldEventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(eventsToCreate, numberOfOldEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(Type aggregateType, int previousEventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(previousEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate();
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(eventsToCreate, startEvent, endEvent);
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsWithExpectedEventCountTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent, int expectedEventCount)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(eventsToCreate, startEvent, endEvent, expectedEventCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(Type aggregateType, int eventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotSave();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithChanges_SavesAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty();
	}

	[Theory]
	[MemberData(nameof(TooManyEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(Type aggregateType, int eventsToGenerate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(eventsToGenerate);
	}
}
