using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved(CancellationToken cancellationToken)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result.Saved).IsFalse();
		await Assert.That(result.IsValid).IsFalse();
		await Assert.That(((bool)result)).IsFalse();
		await Assert.That(result.ValidationResult.Errors).HasSingleItem();
		await Assert
			.That(result.ValidationResult.Errors.Single().PropertyName)
			.IsEqualTo(nameof(IAggregateTest.IncrementInt32));
	}

	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty(CancellationToken cancellationToken)
	{
		var complexProperty = CreateComplexTestType();
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.SetComplexProperty(complexProperty);
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		var aggregateGetResult = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);

		await Assert.That(aggregateGetResult).IsNotNull();
		await Assert.That(aggregate.ComplexTestType).IsEquivalentTo(aggregateGetResult.ComplexTestType);
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave(CancellationToken cancellationToken)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result).IsFalse();
		fixture.Telemetry.Received(1).SaveContainedNoChanges(aggregateId, Arg.Any<string>(), Arg.Any<string>());
	}

	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate(CancellationToken cancellationToken)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result.Saved).IsTrue();
		await Assert.That(result.Skipped).IsFalse();
		await Assert.That(aggregate.IsNew()).IsFalse();

		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);
		await Assert.That(aggregateFromEventStore).IsNotNull();
		await Assert.That(aggregateFromEventStore.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromEventStore.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromEventStore.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromEventStore.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromEventStore.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);
		await Assert.That(aggregateFromEventStore.Details.Etag).IsEqualTo(aggregate.Details.Etag);
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
		int eventsToGenerate,
		CancellationToken cancellationToken
	)
	{
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(func).Throws<ArgumentOutOfRangeException>();
	}
}
