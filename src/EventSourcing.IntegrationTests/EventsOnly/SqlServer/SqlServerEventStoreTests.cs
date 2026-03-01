namespace Purview.EventSourcing.SqlServer;

[Collection("SqlServer")]
[NCrunch.Framework.Category("SqlServer")]
[NCrunch.Framework.Category("Storage")]
public sealed partial class SqlServerEventStoreTests(SqlServerEventStoreFixture fixture) : IClassFixture<SqlServerEventStoreFixture>
{
	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenDelete_NotifiesChangeFeed(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.DeleteAsync_GivenDelete_NotifiesChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.DeleteAsync_GivenPreviouslySavedAggregate_MarksAsDeleted();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.DeleteAsync_WhenTableStoreConfigRemoveDeletedFromCacheIsTrueAndPreviouslySavedAggregate_RemovesFromCache();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(Type aggregateType, int aggregateCount)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAggregateIdsAsync_GivenNAggregatesInTheStore_CorrectlyReturnsTheirIds(aggregateCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingAll_CorrectlyReturnsAllIds(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(SteppedAggregateCountWithDeletedAggregateIdCountTestData))]
	public async Task GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(Type aggregateType, int nonDeletedAggregateIdCount, int deletedAggregateIdCount)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAggregateIdsAsync_GivenNonDeletedAggregatesAndDeletedAggregatesInTheStoreAndRequestingOnlyNonDeleted_CorrectlyReturnsNonDeletedIdsOnly(nonDeletedAggregateIdCount, deletedAggregateIdCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException();
	}

	[Theory]
	[MemberData(nameof(SnapshotEventCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(Type aggregateType, int eventsToCreate)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(Type aggregateType, int previousEventsToCreate)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetAtAsync_GivenAnAggregateWithSavedEvents_RecreatesAggregateToPreviousVersion(previousEventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetDeletedAsync_GivenDeletedAggregate_ReturnsAggregate();
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetEventRangeAsync_GivenARequestedRangeOfEvents_EventsAreReturnsInCorrectOrder(eventsToCreate, startEvent, endEvent);
	}

	[Theory]
	[MemberData(nameof(RequestedRangeOfEventsWithExpectedEventCountTestData))]
	public async Task GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(Type aggregateType, int eventsToCreate, int startEvent, int? endEvent, int expectedEventCount)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetEventRangeAsync_GivenARequestedRangeOfEvents_GetsEventsRequested(eventsToCreate, startEvent, endEvent, expectedEventCount);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.GetOrCreateAsync_GivenAggregateDoesNotExist_CreatesNewAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.IsDeletedAsync_GivenDeletedAggregates_ReturnsTrue();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.IsDeletedAsync_GivenNonDeletedAggregates_ReturnsFalse();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.RestoreAsync_GivenPreviouslySavedAndDeletedAggregate_MarksAsNotDeleted();
	}

	[Theory]
	[MemberData(nameof(SteppedCountTestData))]
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(Type aggregateType, int eventsToCreate)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(eventsToCreate);
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenAggregateWithNoChanges_DoesNotSave();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenNewAggregateWithChanges_SavesAggregate();
	}

	[Theory]
	[MemberData(nameof(AggregateTestTypes))]
	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(Type aggregateType)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty();
	}

	[Theory]
	[MemberData(nameof(TooManyEventCountTestData))]
	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(Type aggregateType, int eventsToGenerate)
	{
		var tests = CreateSqlServerStoreTests(aggregateType);
		await tests.SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(eventsToGenerate);
	}
}
