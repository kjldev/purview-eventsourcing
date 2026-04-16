using Purview.EventSourcing.SqlServer.Exceptions;

namespace Purview.EventSourcing.SqlServer;

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
}
