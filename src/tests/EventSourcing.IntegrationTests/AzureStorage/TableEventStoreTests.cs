using Purview.EventSourcing.Fixtures.AzureStorage;

namespace Purview.EventSourcing.AzureStorage;

[ClassDataSource<TableEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed partial class TableEventStoreTests(TableEventStoreFixture fixture)
{
	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenDelete_NotifiesChangeFeed(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
		Type aggregateType,
		int aggregateCount,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(
			aggregateCount,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
		Type aggregateType,
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(
			nonDeletedAggregateIdCount,
			deletedAggregateIdCount,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
		Type aggregateType,
		int nonDeletedAggregateIdCount,
		int deletedAggregateIdCount,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(
			nonDeletedAggregateIdCount,
			deletedAggregateIdCount,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetSnapshotEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
		Type aggregateType,
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
			eventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		Type aggregateType,
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
			eventsToCreate,
			numberOfOldEventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
		Type aggregateType,
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
			eventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedEventCountWithOldEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
		Type aggregateType,
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
			eventsToCreate,
			numberOfOldEventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
		Type aggregateType,
		int previousEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(
			previousEventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(RequestedRangeOfEventsTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
		Type aggregateType,
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(
			eventsToCreate,
			startEvent,
			endEvent,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(RequestedRangeOfEventsWithExpectedEventCountTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
		Type aggregateType,
		int eventsToCreate,
		int startEvent,
		int? endEvent,
		int expectedEventCount,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(
			eventsToCreate,
			startEvent,
			endEvent,
			expectedEventCount,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(
		Type aggregateType,
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(
			eventsToCreate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(cancellationToken);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(
		Type aggregateType,
		int eventsToGenerate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(
			eventsToGenerate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(HighEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(
		Type aggregateType,
		int eventsToGenerate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(
			eventsToGenerate,
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(GetAggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(
		Type aggregateType,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(
			cancellationToken
		);
	}

	[Test]
	[MethodDataSource(nameof(TooManyEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
		Type aggregateType,
		int eventsToGenerate,
		CancellationToken cancellationToken
	)
	{
		var tableEventStoreTests = CreateTableStoreTests(aggregateType);

		await tableEventStoreTests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
			eventsToGenerate,
			cancellationToken
		);
	}
}
