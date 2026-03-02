namespace Purview.EventSourcing.MongoDB;

[ClassDataSource<MongoDBEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed partial class MongoDBEventStoreTests(MongoDBEventStoreFixture fixture)
{
	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenDelete_NotifiesChangeFeed();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(
		Type aggregateType
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache();
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
		Type aggregateType,
		int aggregateCount
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
			aggregateCount
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
		Type aggregateType,
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
			nonDeletedAggregateIdCount,
			deletedAggregateIdCount
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
		Type aggregateType,
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
			nonDeletedAggregateIdCount,
			deletedAggregateIdCount
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
		Type aggregateType
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException();
	}

	[Test]
	[MethodDataSource(nameof(SnapshotEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
		Type aggregateType,
		int eventsToCreate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
			eventsToCreate
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		Type aggregateType,
		int eventsToCreate,
		int numberOfOldEventsToCreate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
			eventsToCreate,
			numberOfOldEventsToCreate
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
		Type aggregateType,
		int eventsToCreate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
			eventsToCreate
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
		Type aggregateType,
		int eventsToCreate,
		int numberOfOldEventsToCreate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
			eventsToCreate,
			numberOfOldEventsToCreate
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		Type aggregateType,
		int previousEventsToCreate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
			previousEventsToCreate
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate();
	}

	[Test]
	[MethodDataSource(nameof(RequestedRangeOfEventsTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
		Type aggregateType,
		int eventsToCreate,
		int startEvent,
		int? endEvent
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
			eventsToCreate,
			startEvent,
			endEvent
		);
	}

	[Test]
	[MethodDataSource(nameof(RequestedRangeOfEventsWithExpectedEventCountTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
		Type aggregateType,
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		int expectedEventCount
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
			eventsToCreate,
			startEvent,
			endEvent,
			expectedEventCount
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted();
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(Type aggregateType, int eventsToCreate)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(eventsToCreate);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(
		Type aggregateType
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotSave();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithChanges_SavesAggregate();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(
		Type aggregateType
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord();
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(Type aggregateType)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty();
	}

	[Test]
	[MethodDataSource(nameof(TooManyEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
		Type aggregateType,
		int eventsToGenerate
	)
	{
		var mongoDBEventStoreTests = CreateMongoDBStoreTests(aggregateType);

		await mongoDBEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
			eventsToGenerate
		);
	}
}
