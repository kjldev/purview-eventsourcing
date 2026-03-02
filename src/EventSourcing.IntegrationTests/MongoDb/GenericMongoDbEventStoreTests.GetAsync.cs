using Purview.EventSourcing.Aggregates.Persistence.Events;
using Purview.EventSourcing.MongoDB.Entities;
using Purview.EventSourcing.MongoDB.Exceptions;
using Purview.EventSourcing.MongoDB.StorageClients;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var func = () =>
			eventStore.GetAsync(
				aggregateId,
				new EventStoreOperationContext { DeleteMode = DeleteHandlingMode.ThrowsException },
				cancellationToken: tokenSource.Token
			);

		// Assert
		await Assert.That(func).Throws<AggregateIsDeletedException>();
	}

	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(int eventsToCreate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var snapshotEntity = await fixture.SnapshotClient.GetAsync<SnapshotEntity>(
			aggregateId,
			EntityTypes.SnapshotType,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(snapshotEntity).IsNotNull();

		await fixture.SnapshotClient.DeleteAsync<SnapshotEntity>(
			m => m.Id == aggregateId,
			cancellationToken: tokenSource.Token
		);

		snapshotEntity = await fixture.SnapshotClient.GetAsync<SnapshotEntity>(
			aggregateId,
			EntityTypes.SnapshotType,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(snapshotEntity).IsNull();

		// Assert
		var result = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

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

	public async Task GetAsync_GivenAnAggregateWithMoreEventsThanTheSnapshot_RecreatesAggregate(int eventsToCreate)
	{
		// Arrange
		const int snapshotInterval = 5;
		const int eventCountOffset = snapshotInterval - 1;

		var expectedSnapshotVersion = eventsToCreate - eventCountOffset;
		var initialEventsToCreate = eventsToCreate - eventCountOffset;

		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";

		using var eventStore = fixture.CreateEventStore<TAggregate>(snapshotRecalculationInterval: snapshotInterval);

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

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.IncrementInt32).IsEqualTo(eventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(eventsToCreate);
		await Assert.That(result.Details.SnapshotVersion).IsEqualTo(expectedSnapshotVersion);
	}

	// This is testing that the aggregate is still correct after having an event type removed (in this case,
	// it deserializes, but it's not registered any longer),
	// this is often due to the schema changes and the event not being required anymore, but the
	// event record still (correctly) exists.
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		int eventsToCreate,
		int numberOfOldEventsToCreate
	)
	{
		// Arrange
		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: tokenSource.Token
		);

		// Assert
		fixture
			.Telemetry.Received(numberOfOldEventsToCreate)
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
		int numberOfOldEventsToCreate
	)
	{
		// Arrange
		const string unknownEventType = "an-unknown-type";

		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;

		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		// Register the event type here...!
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Update existing events to make them unknown types effectively.
		var eventsToUpdate = eventStore.GetEventRangeEntitiesAsync(
			aggregateId,
			eventsToCreate + 1,
			totalEvents,
			tokenSource.Token
		);

		BatchOperation batchOperation = new();
		var batch = batchOperation;
		await foreach (var eventToUpdate in eventsToUpdate)
		{
			eventToUpdate.EventType = unknownEventType;

			batch.Update(eventToUpdate);
		}

		await fixture.EventClient.SubmitBatchAsync(batch, tokenSource.Token);

		// Get without using the snapshot, just from the event record.
		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: tokenSource.Token
		);

		// Assert
		fixture
			.Telemetry.Received(numberOfOldEventsToCreate)
			.SkippedUnknownEvent(aggregateId, Arg.Any<string>(), Arg.Any<string>(), unknownEventType, Arg.Any<int>());

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(result.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(totalEvents);
		await Assert.That(result.Details.CurrentVersion).IsEqualTo(totalEvents);
	}
}
