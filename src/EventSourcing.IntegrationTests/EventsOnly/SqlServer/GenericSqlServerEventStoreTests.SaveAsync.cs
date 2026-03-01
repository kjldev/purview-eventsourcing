using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		result.Saved.ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
		((bool)result).ShouldBeFalse();
		result.ValidationResult.Errors.ShouldHaveSingleItem();
		result.ValidationResult.Errors.Single().PropertyName.ShouldBe(nameof(IAggregateTest.IncrementInt32));
	}

	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var complexProperty = CreateComplexTestType();
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.SetComplexProperty(complexProperty);
		using var eventStore = fixture.CreateEventStore<TAggregate>();
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		var aggregateGetResult = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		aggregateGetResult.ShouldNotBeNull();
		aggregate.ComplexTestType.ShouldBeEquivalentTo(aggregateGetResult.ComplexTestType);
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		result.ShouldBeFalse();
		fixture.Telemetry.Received(1).SaveContainedNoChanges(aggregateId, Arg.Any<string>(), Arg.Any<string>());
	}

	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate()
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		result.Saved.ShouldBeTrue();
		result.Skipped.ShouldBeFalse();
		aggregate.IsNew().ShouldBeFalse();

		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);
		aggregateFromEventStore.ShouldNotBeNull();
		aggregateFromEventStore.Id().ShouldBe(aggregate.Id());
		aggregateFromEventStore.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromEventStore.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromEventStore.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromEventStore.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);
		aggregateFromEventStore.Details.Etag.ShouldBe(aggregate.Details.Etag);
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(int eventsToGenerate)
	{
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();
		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		await Should.ThrowAsync<ArgumentOutOfRangeException>(func);
	}
}
