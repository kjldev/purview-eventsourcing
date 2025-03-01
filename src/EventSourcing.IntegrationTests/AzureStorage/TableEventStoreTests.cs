namespace Purview.EventSourcing.AzureStorage;

[Collection("AzureStorage")]
[NCrunch.Framework.Category("AzureStorage")]
[NCrunch.Framework.Category("Storage")]
public sealed partial class TableEventStoreTests(TableEventStoreFixture fixture) : IClassFixture<TableEventStoreFixture>
{
	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenDelete_NotifiesChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(Type aggregateType, int aggregateCount)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(aggregateCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException();
	}

	[Theory]
	[MemberData(nameof(SnapshotEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(Type aggregateType, int eventsToCreate, int numberOfOldEventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(eventsToCreate, numberOfOldEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(Type aggregateType, int eventsToCreate, int numberOfOldEventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(eventsToCreate, numberOfOldEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(Type aggregateType, int previousEventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(previousEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate();
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(eventsToCreate, startEvent, endEvent);
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsWithExpectedEventCountTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent, int expectedEventCount)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(eventsToCreate, startEvent, endEvent, expectedEventCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(Type aggregateType, int eventsToCreate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotSave();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithChanges_SavesAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(Type aggregateType, int eventsToGenerate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(eventsToGenerate);
	}

	[Theory]
	[MemberData(nameof(HighEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(Type aggregateType, int eventsToGenerate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(eventsToGenerate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(Type aggregateType)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty();
	}

	[Theory]
	[MemberData(nameof(TooManyEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(Type aggregateType, int eventsToGenerate)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(eventsToGenerate);
	}
}
