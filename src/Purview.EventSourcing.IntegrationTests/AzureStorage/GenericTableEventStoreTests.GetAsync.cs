using Purview.EventSourcing.Aggregates.Persistence.Events;
using Purview.EventSourcing.AzureStorage.Exceptions;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var func = () => eventStore.GetAsync(aggregateId, new EventStoreOperationContext
		{
			DeleteMode = DeleteHandlingMode.ThrowsException
		}, cancellationToken: tokenSource.Token);

		// Assert
		await func
			.Should()
			.ThrowExactlyAsync<AggregateIsDeletedException>();
	}

	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(int eventsToCreate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var blobName = eventStore.GenerateSnapshotBlobName(aggregateId);
		var exists = await fixture.BlobClient.ExistsAsync(blobName, cancellationToken: tokenSource.Token);

		exists.ShouldBeTrue();

		await fixture.BlobClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: tokenSource.Token);

		exists = await fixture.BlobClient.ExistsAsync(blobName, cancellationToken: tokenSource.Token);

		exists.ShouldBeFalse();

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		result
			.Should()
			.NotBeNull();

		result!
			.IsNew()
			.Should()
			.BeFalse();

		result!.Id()
			.Should()
			.Be(aggregate.Id());

		result!.IncrementInt32
			.Should()
			.Be(aggregate.IncrementInt32);

		result!.Details.SavedVersion
			.Should()
			.Be(aggregate.Details.SavedVersion);

		result!.Details.CurrentVersion
			.Should()
			.Be(aggregate.Details.CurrentVersion);

		result!.Details.Etag
			.Should()
			.Be(aggregate.Details.Etag);

		result!.Details.SnapshotVersion
			.Should()
			.Be(0, "There is no snapshot version as it was deleted as part of this test.");
	}

	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(int eventsToCreate)
	{
		// Arrange
		const int snapshotInterval = 5;
		const int eventCountOffset = snapshotInterval - 1;

		var expectedSnapshotVersion = eventsToCreate - eventCountOffset;
		var initialEventsToCreate = eventsToCreate - eventCountOffset;

		using var tokenSource = TestHelpers.CancellationTokenSource();

		var aggregateId = $"{Guid.NewGuid()}";

		var eventStore = fixture.CreateEventStore<TAggregate>(snapshotRecalculationInterval: snapshotInterval);

		// Act
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < initialEventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		for (var i = 0; i < eventCountOffset; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		result
			.Should()
			.NotBeNull();

		result!
			.IsNew()
			.Should()
			.BeFalse();

		result!
			.IncrementInt32
			.Should()
			.Be(eventsToCreate);

		result!
			.Details
			.SavedVersion
			.Should()
			.Be(eventsToCreate);

		result!
			.Details
			.SnapshotVersion
			.Should()
			.Be(expectedSnapshotVersion);
	}

	// This is testing that the aggregate is still correct after having an event type removed (in this case,
	// it deserializes, but it's not registered any longer),
	// this is often due to the schema changes and the event not being required anymore, but the
	// event record still (correctly) exists.
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(int eventsToCreate, int numberOfOldEventsToCreate)
	{
		// Arrange
		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		using var tokenSource = TestHelpers.CancellationTokenSource();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(aggregateId, new EventStoreOperationContext
		{
			SkipSnapshot = true
		}, cancellationToken: tokenSource.Token);

		// Assert
		fixture
			.Telemetry
			.Received(numberOfOldEventsToCreate)
			.CannotApplyEvent(aggregateId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<string>(eventType => eventType.Contains(typeof(OldEvent).Name, StringComparison.Ordinal)), Arg.Any<int>());

		result
			.Should()
			.NotBeNull();

		result!
			.IsNew()
			.Should()
			.BeFalse();

		result!.Id()
			.Should()
			.Be(aggregate.Id());

		result!
			.IncrementInt32
			.Should()
			.Be(aggregate.IncrementInt32);

		result!
			.Details.SavedVersion
			.Should()
			.Be(totalEvents);

		result!
			.Details.CurrentVersion
			.Should()
			.Be(totalEvents);
	}

	// This is testing that the aggregate is still correct after an event type cannot be found - removed 
	// from the assembly/ failure to load the type -
	// this is often due to the schema changes and the event not being required anymore, but the
	// event record still (correctly) exists.
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(int eventsToCreate, int numberOfOldEventsToCreate)
	{
		// Arrange
		const string unknownEventType = "an-unknown-type";

		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		using var tokenSource = TestHelpers.CancellationTokenSource();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Update existing events to make them unknown types effectively.
		var eventsToUpdate = eventStore.GetEventRangeEntitiesAsync(aggregateId, eventsToCreate + 1, totalEvents, tokenSource.Token);

		BatchOperation batchOperation = new();
		var batch = batchOperation;
		await foreach (var eventToUpdate in eventsToUpdate)
		{
			eventToUpdate.EventType = unknownEventType;

			batch.Update(eventToUpdate, merge: false);
		}

		await fixture.TableClient.SubmitBatchAsync(batch, tokenSource.Token);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(aggregateId, new EventStoreOperationContext
		{
			SkipSnapshot = true
		}, cancellationToken: tokenSource.Token);

		// Assert
		fixture
			.Telemetry
			.Received(numberOfOldEventsToCreate)
			.SkippedUnknownEvent(aggregateId, Arg.Any<string>(), Arg.Any<string>(), unknownEventType, Arg.Any<int>());

		result
			.Should()
			.NotBeNull();

		result!
			.IsNew()
			.Should()
			.BeFalse();

		result!.Id()
			.Should()
			.Be(aggregate.Id());

		result!
			.IncrementInt32
			.Should()
			.Be(aggregate.IncrementInt32);

		result!
			.Details.SavedVersion
			.Should()
			.Be(totalEvents);

		result!
			.Details.CurrentVersion
			.Should()
			.Be(totalEvents);
	}
}
