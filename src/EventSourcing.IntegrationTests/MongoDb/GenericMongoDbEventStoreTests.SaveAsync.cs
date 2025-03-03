using System.Text;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.Saved.ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
		((bool)result).ShouldBeFalse();
		result.ValidationResult.Errors.ShouldHaveSingleItem();
		result.ValidationResult.Errors.Single().PropertyName.ShouldBe(nameof(IAggregateTest.IncrementInt32));
	}

	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var complexProperty = CreateComplexTestType();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		aggregate.SetComplexProperty(complexProperty);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var aggregateGetResult = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		aggregateGetResult.ShouldNotBeNull();
		aggregate.ComplexTestType.ShouldBeEquivalentTo(aggregateGetResult.ComplexTestType);
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeFalse();

		fixture
			.Telemetry
			.Received(1)
			.SaveContainedNoChanges(aggregateId, Arg.Any<string>(), Arg.Any<string>());
	}

	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.Saved.ShouldBeTrue();
		result.Skipped.ShouldBeFalse();

		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		aggregateFromEventStore.ShouldNotBeNull();
		aggregateFromEventStore.Id().ShouldBe(aggregate.Id());
		aggregateFromEventStore.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromEventStore.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromEventStore.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromEventStore.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);
		aggregateFromEventStore.Details.Etag.ShouldBe(aggregate.Details.Etag);
	}

	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		var value = string.Empty;
		var sizeIsLessThan32K = true;
		while (sizeIsLessThan32K)
		{
			value += "abcdefghijklmnopqrstvwxyz";
			value += "ABCDEFGHIJKLMNOPQRSTVWXYZ";
			value += "1234567890";

			sizeIsLessThan32K = Encoding.UTF8.GetByteCount(value) < short.MaxValue;
		}

		aggregate.AppendString(value);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		(aggregateFromEventStore?.StringProperty ?? string.Empty)
			.Length.ShouldBe(aggregate.StringProperty.Length);

		aggregateFromEventStore?
			.StringProperty
			.ShouldBe(aggregate.StringProperty);

		sizeIsLessThan32K = Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty) < short.MaxValue;
		sizeIsLessThan32K.ShouldBeFalse();
	}

	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		var value = string.Empty;
		var sizeIsLessThan32K = true;
		while (sizeIsLessThan32K)
		{
			value += "abcdefghijklmnopqrstvwxyz";
			value += "ABCDEFGHIJKLMNOPQRSTVWXYZ";
			value += "1234567890";

			sizeIsLessThan32K = Encoding.UTF8.GetByteCount(value) < short.MaxValue;
		}

		aggregate.AppendString(value);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Delete the snapshot to ensure the events are replayed.
		await fixture.SnapshotClient.DeleteAsync<SnapshotEntity>(m => m.Id == aggregateId, cancellationToken: tokenSource.Token);

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		(aggregateFromEventStore?.StringProperty ?? string.Empty)
			.Length.ShouldBe(aggregate.StringProperty.Length);

		aggregateFromEventStore?
			.StringProperty
			.ShouldBe(aggregate.StringProperty);

		sizeIsLessThan32K = Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty) < short.MaxValue;
		sizeIsLessThan32K.ShouldBeFalse();
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(int eventsToGenerate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		await Should.ThrowAsync<ArgumentOutOfRangeException>(func);
	}
}
