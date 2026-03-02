using Purview.EventSourcing.SqlServer.Exceptions;

namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task GetAsync_GivenAggregateIsDeletedAndDeletedModeIsSetToThrow_ThrowsEventStoreAggregateDeletedException()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		await eventStore.DeleteAsync(aggregate, cancellationToken: tokenSource.Token);

		var func = () =>
			eventStore.GetAsync(
				aggregateId,
				new EventStoreOperationContext { DeleteMode = DeleteHandlingMode.ThrowsException },
				cancellationToken: tokenSource.Token
			);

		await Assert.That(func).Throws<AggregateIsDeletedException>();
	}

	public async Task GetAsync_GivenAnAggregateWithSavedEventsButNoSnapshot_RecreatesAggregate(int eventsToCreate)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Delete the snapshot directly from SQL Server
		var snapshotId = $"snap_{aggregate.AggregateType}_{aggregateId}";
		await fixture.Client.DeleteByIdAsync(snapshotId, cancellationToken: tokenSource.Token);

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
		const int snapshotInterval = 5;
		const int eventCountOffset = snapshotInterval - 1;
		var expectedSnapshotVersion = eventsToCreate - eventCountOffset;
		var initialEventsToCreate = eventsToCreate - eventCountOffset;
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);
		var aggregateId = $"{Guid.NewGuid()}";
		using var eventStore = fixture.CreateEventStore<TAggregate>(snapshotRecalculationInterval: snapshotInterval);

		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < initialEventsToCreate; i++)
			aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);
		for (var i = 0; i < eventCountOffset; i++)
			aggregate.IncrementInt32Value();
		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var result = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert.That(result).IsNotNull();
		await Assert.That(result.IsNew()).IsFalse();
		await Assert.That(result.IncrementInt32).IsEqualTo(eventsToCreate);
		await Assert.That(result.Details.SavedVersion).IsEqualTo(eventsToCreate);
		await Assert.That(result.Details.SnapshotVersion).IsEqualTo(expectedSnapshotVersion);
	}
}
