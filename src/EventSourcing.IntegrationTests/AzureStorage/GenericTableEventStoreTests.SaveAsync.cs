using System.Text;
using Azure.Data.Tables;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.AzureStorage.Entities;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithDataAnnotationsAndInvalidProperties_NoChangesAreMadeAndNotSaved()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var aggregateId = $"{Guid.NewGuid()}";
		var complexProperty = CreateComplexTestType();
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		aggregate.SetComplexProperty(complexProperty);

		var eventStore = fixture.CreateEventStore<TAggregate>();
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();
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

	public async Task SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		var streamVersion =
			await fixture.TableClient.GetAsync<TableEntity>(
				aggregateId,
				TableEventStoreConstants.StreamVersionRowKey,
				cancellationToken: tokenSource.Token
			) ?? throw new NullReferenceException();
		var streamVersionVersion = streamVersion[nameof(StreamVersionEntity.Version)] as int?;

		streamVersion.Remove(nameof(StreamVersionEntity.Version));

		await fixture.TableClient.OperationAsync(
			TableTransactionActionType.UpdateReplace,
			streamVersion,
			cancellationToken: tokenSource.Token
		);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert.That(aggregateFromEventStore).IsNotNull();
		await Assert.That(aggregateFromEventStore.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromEventStore.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromEventStore.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromEventStore.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromEventStore.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);

		await Assert.That(streamVersionVersion).IsEqualTo(eventsToGenerate);
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Delete the snapshot to ensure the events are replayed.
		var blobName = eventStore.GenerateSnapshotBlobName(aggregateId);
		var deleteResult = await fixture.BlobClient.DeleteBlobIfExistsAsync(
			blobName,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(deleteResult).IsTrue();

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

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(
		int eventsToGenerate
	)
	{
		// Minus 2 is because we also add the idempotency marker and stream on the first batch.
		if (eventsToGenerate < (StorageClients.Table.AzureTableClient.MaximumBatchSize - 2))
			await Assert
				.That(
					$"'{eventsToGenerate}' should be greater than {StorageClients.Table.AzureTableClient.MaximumBatchSize}."
				)
				.IsNull();

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(
			cancellationToken: TestContext.Current.Execution.CancellationToken
		);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		var streamVersion = await fixture.TableClient.GetAsync<TableEntity>(
			aggregateId,
			TableEventStoreConstants.StreamVersionRowKey,
			cancellationToken: tokenSource.Token
		);

		await Assert.That(streamVersion).IsNotNull();

		var streamVersionVersion = streamVersion![nameof(StreamVersionEntity.Version)] as int?;

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.IsNew()).IsFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		await Assert.That(aggregateFromEventStore).IsNotNull();
		await Assert.That(aggregateFromEventStore.Id()).IsEqualTo(aggregate.Id());
		await Assert.That(aggregateFromEventStore.IncrementInt32).IsEqualTo(aggregate.IncrementInt32);
		await Assert.That(aggregateFromEventStore.Details.SavedVersion).IsEqualTo(aggregate.Details.SavedVersion);
		await Assert.That(aggregateFromEventStore.Details.CurrentVersion).IsEqualTo(aggregate.Details.CurrentVersion);
		await Assert.That(aggregateFromEventStore.Details.SnapshotVersion).IsEqualTo(aggregate.Details.SnapshotVersion);

		await Assert.That(streamVersionVersion).IsEqualTo(eventsToGenerate);
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		await Assert.That(func).Throws<ArgumentOutOfRangeException>();
	}
}
