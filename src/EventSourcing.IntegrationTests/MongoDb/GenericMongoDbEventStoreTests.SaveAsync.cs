using System.Text;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result.Saved).IsFalse();
		await Assert.That(result.IsValid).IsFalse();
		await Assert.That(((bool)result)).IsFalse();
		await Assert.That(result.ValidationResult.Errors).HasSingleItem();
		await Assert
			.That(result.ValidationResult.Errors.Single().PropertyName)
			.IsEqualTo(nameof(IAggregateTest.IncrementInt32));
	}

	public async Task SaveAsync_GivenAggregateWithComplexProperty_SavesEventWithComplexProperty()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var complexProperty = CreateComplexTestType();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		aggregate.SetComplexProperty(complexProperty);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Act
		var aggregateGetResult = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(aggregateGetResult).IsNotNull();
		await Assert.That(aggregate.ComplexTestType).IsEquivalentTo(aggregateGetResult.ComplexTestType);
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotSave()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsFalse();

		fixture.Telemetry.Received(1).SaveContainedNoChanges(aggregateId, Arg.Any<string>(), Arg.Any<string>());
	}

	public async Task SaveAsync_GivenNewAggregateWithChanges_SavesAggregate()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result.Saved).IsTrue();
		await Assert.That(result.Skipped).IsFalse();

		await Assert.That(aggregate.IsNew()).IsFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert.That(aggregateFromEventStore).IsNotNull();
		await Assert.That(aggregateFromEventStore.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromEventStore.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromEventStore.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromEventStore.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromEventStore.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);
		await Assert.That(aggregateFromEventStore.Details.Etag).IsEqualTo(aggregate.Details.Etag);
	}

	public async Task SaveAsync_GivenNewAggregateWithLargeChanges_SavesAggregateWithLargeEventRecord()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

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
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert
			.That((aggregateFromEventStore?.StringProperty ?? string.Empty).Length)
			.IsEqualTo(aggregate.StringProperty.Length);

		await Assert.That(aggregateFromEventStore?.StringProperty).IsEqualTo(aggregate.StringProperty);

		sizeIsLessThan32K =
			Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty) < short.MaxValue;
		await Assert.That(sizeIsLessThan32K).IsFalse();
	}

	public async Task SaveAsync_GivenNewAggregateWithLargeChangesAndNoSnapshot_ReadsAggregateFromEvents()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

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
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Delete the snapshot to ensure the events are replayed.
		await fixture.SnapshotClient.DeleteAsync<SnapshotEntity>(
			m => m.Id == aggregateId,
			cancellationToken: tokenSource.Token
		);

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert
			.That((aggregateFromEventStore?.StringProperty ?? string.Empty).Length)
			.IsEqualTo(aggregate.StringProperty.Length);

		await Assert.That(aggregateFromEventStore?.StringProperty).IsEqualTo(aggregate.StringProperty);

		sizeIsLessThan32K =
			Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty) < short.MaxValue;
		await Assert.That(sizeIsLessThan32K).IsFalse();
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(
		int eventsToGenerate
	)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		await Assert.That(func).Throws<ArgumentOutOfRangeException>();
	}
}
