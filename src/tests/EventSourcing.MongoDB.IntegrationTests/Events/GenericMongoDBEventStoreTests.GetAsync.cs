using Purview.EventSourcing.Aggregates.Persistence.Events;
using Purview.EventSourcing.MongoDB.Events.Entities;
using Purview.EventSourcing.MongoDB.Events.Exceptions;
using Purview.EventSourcing.MongoDB.StorageClient;

namespace Purview.EventSourcing.MongoDB.Events;

partial class GenericMongoDBEventStoreTests<TAggregate>
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
		Task<TAggregate?> Func() =>
			eventStore.GetAsync(
				aggregateId,
				new EventStoreOperationContext { DeleteMode = DeleteHandlingMode.ThrowsException },
				cancellationToken: cancellationToken
			);

		// Assert
		await Assert.That(Func).Throws<AggregateIsDeletedException>();
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
		var snapshotClient = ctx.SnapshotClient;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Act
		var snapshotEntity = await snapshotClient.GetAsync<SnapshotEntity>(
			aggregateId,
			EntityTypes.SnapshotType,
			cancellationToken: cancellationToken
		);

		await Assert.That(snapshotEntity).IsNotNull();

		await snapshotClient.DeleteAsync<SnapshotEntity>(
			m => m.Id == aggregateId,
			cancellationToken: cancellationToken
		);

		snapshotEntity = await snapshotClient.GetAsync<SnapshotEntity>(
			aggregateId,
			EntityTypes.SnapshotType,
			cancellationToken: cancellationToken
		);

		await Assert.That(snapshotEntity).IsNull();

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(result.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(result.Details.Etag).IsEqualTo(aggregate.Details.Etag);
		await Assert
			.That(result.Details.SnapshotVersion)
			.IsEqualTo(0)
			.Because("There is no snapshot version as it was deleted as part of this test.");
	}

	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// The MongoDB event store snapshots on every save that persists events.
		const int eventCountOffset = 4;
		var expectedSnapshotVersion = eventsToCreate + eventCountOffset;
		var totalEventsToCreate = eventsToCreate + eventCountOffset;

		var aggregateId = $"{Guid.NewGuid()}";

		var eventStore = fixture.CreateEventStore<TAggregate>();

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
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.IncrementInt32).IsEqualTo(totalEventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(totalEventsToCreate);
		await Assert.That(result.Details.SnapshotVersion).IsEqualTo(expectedSnapshotVersion);
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
		var eventClient = ctx.EventClient;
		var telemetry = ctx.Telemetry;

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

			batch.Update(eventToUpdate);
		}

		await eventClient.SubmitBatchAsync(batch, cancellationToken);

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
