using Purview.EventSourcing.Aggregates.Persistence.Events;
using Purview.EventSourcing.SqlServer.Events.Exceptions;

namespace Purview.EventSourcing.SqlServer.Events;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException(
		CancellationToken cancellationToken
	)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		await eventStore.DeleteAsync(aggregate, cancellationToken: cancellationToken);

		Task<TAggregate?> Func() =>
			eventStore.GetAsync(
				aggregateId,
				new EventStoreOperationContext { DeleteMode = DeleteHandlingMode.ThrowsException },
				cancellationToken: cancellationToken
			);

		await Assert.That(Func).Throws<AggregateIsDeletedException>();
	}

	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();
		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Delete the snapshot directly from SQL Server
		var snapshotId = $"snap_{aggregate.AggregateType}_{aggregateId}";
		await ctx.Client.DeleteByIdAsync(snapshotId, cancellationToken: cancellationToken);

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

		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);
		for (var i = 0; i < eventCountOffset; i++)
			aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.IncrementInt32).IsEqualTo(totalEventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(totalEventsToCreate);
		await Assert.That(result.Details.SnapshotVersion).IsEqualTo(expectedSnapshotVersion);
	}

	// This is testing that the aggregate is still correct after having an event type removed (in this case,
	// it deserializes, but it's not registered any longer),
	// this is often due to schema changes and the event no longer being required, but the
	// event record still correctly existing.
	public async Task GetAsync_GivenAnAggregateWithNonRegisteredEventType_RecreatesAggregateAndLogsCannotApplyEvent(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		var telemetry = ctx.Telemetry;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: cancellationToken
		);

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

	// This is testing that the aggregate is still correct after an event type cannot be found
	// due to schema/version evolution while historical records still exist.
	public async Task GetAsync_GivenAnAggregateWithUnknownEventType_RecreatesAggregateAndLogsUnknown(
		int eventsToCreate,
		int numberOfOldEventsToCreate,
		CancellationToken cancellationToken
	)
	{
		const string unknownEventType = "an-unknown-type";
		var totalEvents = eventsToCreate + numberOfOldEventsToCreate;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.RegisterOldEventType();

		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		for (var i = 0; i < numberOfOldEventsToCreate; i++)
			aggregate.SetOldEventValue(Guid.NewGuid());

		var ctx = fixture.CreateEventStoreContext<TAggregate>();
		var eventStore = ctx.EventStore;
		var client = ctx.Client;
		var telemetry = ctx.Telemetry;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await foreach (
			var row in client.GetEventRangeAsync(
				aggregateId,
				aggregate.AggregateType,
				eventsToCreate + 1,
				totalEvents,
				cancellationToken
			)
		)
		{
			row.EventType = unknownEventType;
			await client.UpsertAsync(
				row.Id,
				row.EntityType,
				row.AggregateId,
				row.AggregateType,
				row.Version,
				row.IsDeleted,
				row.Payload,
				row.EventType,
				row.IdempotencyId,
				row.Timestamp,
				cancellationToken
			);
		}

		var result = await eventStore.GetAsync(
			aggregateId,
			new EventStoreOperationContext { SkipSnapshot = true },
			cancellationToken: cancellationToken
		);

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
