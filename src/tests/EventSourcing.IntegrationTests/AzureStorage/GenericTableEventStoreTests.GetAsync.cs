using Purview.EventSourcing.Aggregates.Persistence.Events;
using Purview.EventSourcing.AzureStorage.Exceptions;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var func = () =>
			eventStore.GetAsync(
				aggregateId,
				new EventStoreOperationContext { DeleteMode = DeleteHandlingMode.ThrowsException },
				cancellationToken: cancellationToken
			);

		// Assert
		await Assert.That(func).Throws<AggregateIsDeletedException>();
	}

	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		var blobClient = ctx.BlobClient;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var blobName = eventStore.GenerateSnapshotBlobName(aggregateId);
		var exists = await blobClient.ExistsAsync(blobName, cancellationToken: cancellationToken);

		await Assert.That(exists).IsTrue();

		await blobClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);

		exists = await blobClient.ExistsAsync(blobName, cancellationToken: cancellationToken);

		await Assert.That(exists).IsFalse();

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();

		await Assert.That(result!.IsNew()).IsFalse();
		await Assert.That(result!.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(result!.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(result!.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(result!.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(result!.Details.Etag).IsEqualTo(aggregate.Details.Etag);
		await Assert
			.That(result!.Details.SnapshotVersion)
			.IsEqualTo(0)
			.Because("There is no snapshot version as it was deleted as part of this test.");
	}

	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// eventsToCreate is a multiple of snapshotInterval (10, 20, 50, 80, 100).
		// First save triggers a snapshot at CurrentVersion == eventsToCreate.
		// Second save adds eventCountOffset more events (not reaching the next interval),
		// so no new snapshot is taken and the aggregate must be reconstructed from
		// the snapshot plus the trailing events.
		const int snapshotInterval = 5;
		const int eventCountOffset = snapshotInterval - 1;

		var expectedSnapshotVersion = eventsToCreate;
		var totalEventsToCreate = eventsToCreate + eventCountOffset;

		var aggregateId = $"{Guid.NewGuid()}";
		var eventStore = fixture.CreateEventStore<TAggregate>(snapshotRecalculationInterval: snapshotInterval);

		// Act
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		for (var i = 0; i < eventCountOffset; i++)
			aggregate.IncrementInt32Value();

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();
		await Assert.That(result!.IsNew()).IsFalse();
		await Assert.That(result!.IncrementInt32).IsEqualTo(totalEventsToCreate);
		await Assert.That(result!.Details.SavedVersion).IsEqualTo(totalEventsToCreate);
		await Assert.That(result!.Details.SnapshotVersion).IsEqualTo(expectedSnapshotVersion);
	}

	// This is testing that the aggregate is still correct after having an event type removed (in this case,
	// it deserializes, but it's not registered any longer),
	// this is often due to the schema changes and the event not being required anymore, but the
	// event record still (correctly) exists.
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		var telemetry = ctx.Telemetry;

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: cancellationToken
		);

		// Assert
		telemetry
			.Received(numberOfOldEventsToCreate)
			.CannotApplyEvent(
				aggregateId,
				Arg.Any<string>(),
				Arg.Any<string>(),
				Arg.Any<string>(),
				Arg.Is<string>(eventType => eventType.Contains(typeof(OldEvent).Name, StringComparison.Ordinal)),
				Arg.Any<int>()
			);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(result.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(totalEvents);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(totalEvents);
	}

	// This is testing that the aggregate is still correct after an event type cannot be found - removed
	// from the assembly/ failure to load the type -
	// this is often due to the schema changes and the event not being required anymore, but the
	// event record still (correctly) exists.
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string unknownEventType = "an-unknown-type";

		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		var telemetry = ctx.Telemetry;
		var tableClient = ctx.TableClient;

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Update existing events to make them unknown types effectively.
		var eventsToUpdate = eventStore.GetEventRangeEntitiesAsync(
			aggregateId,
			eventsToCreate + 1,
			totalEvents,
			cancellationToken
		);

		BatchOperation batchOperation = new();
		var batch = batchOperation;
		await foreach (var eventToUpdate in eventsToUpdate)
		{
			eventToUpdate.EventType = unknownEventType;

			batch.Update(eventToUpdate, merge: false);
		}

		await tableClient.SubmitBatchAsync(batch, cancellationToken);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: cancellationToken
		);

		// Assert
		telemetry
			.Received(numberOfOldEventsToCreate)
			.SkippedUnknownEvent(aggregateId, Arg.Any<string>(), Arg.Any<string>(), unknownEventType, Arg.Any<int>());

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(result.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(totalEvents);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(totalEvents);
	}
}
