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
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId, a => a.SetValidatedProperty(-1));

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var aggregateId = $"{Guid.NewGuid()}";
		var complexProperty = CreateComplexTestType();
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		aggregate.SetComplexProperty(complexProperty);

		var eventStore = fixture.CreateEventStore<TAggregate>();
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
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

	public async Task SaveAsync_GivenStreamVersionWithoutVersionSetWhenSaved_StreamVersionHasCorrectEvent(int eventsToGenerate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		var streamVersion = await fixture.TableClient.GetAsync<TableEntity>(aggregateId, TableEventStoreConstants.StreamVersionRowKey, cancellationToken: tokenSource.Token) ?? throw new NullReferenceException();
		var streamVersionVersion = streamVersion[nameof(StreamVersionEntity.Version)] as int?;

		streamVersion.Remove(nameof(StreamVersionEntity.Version));

		await fixture.TableClient.OperationAsync(TableTransactionActionType.UpdateReplace, streamVersion, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		aggregateFromEventStore.ShouldNotBeNull();
		aggregateFromEventStore.Id().ShouldBe(aggregate.Id());
		aggregateFromEventStore.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromEventStore.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromEventStore.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromEventStore.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);

		streamVersionVersion.ShouldBe(eventsToGenerate);
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

		var eventStore = fixture.CreateEventStore<TAggregate>();

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

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Delete the snapshot to ensure the events are replayed.
		var blobName = eventStore.GenerateSnapshotBlobName(aggregateId);
		var deleteResult = await fixture.BlobClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: tokenSource.Token);

		deleteResult.ShouldBeTrue();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		(aggregateFromEventStore?.StringProperty ?? string.Empty)
			.Length.ShouldBe(aggregate.StringProperty.Length);

		aggregateFromEventStore?.StringProperty.ShouldBe(aggregate.StringProperty);

		sizeIsLessThan32K = Encoding.UTF8.GetByteCount(aggregateFromEventStore?.StringProperty ?? string.Empty) < short.MaxValue;
		sizeIsLessThan32K.ShouldBeFalse();
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedInBatchOperation_BatchesEvents(int eventsToGenerate)
	{
		// Minus 2 is because we also add the idempotency marker and stream on the first batch.
		if (eventsToGenerate < (StorageClients.Table.AzureTableClient.MaximumBatchSize - 2))
			$"'{eventsToGenerate}' should be greater than {StorageClients.Table.AzureTableClient.MaximumBatchSize}.".ShouldBeNull();

		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		var streamVersion = await fixture.TableClient.GetAsync<TableEntity>(aggregateId, TableEventStoreConstants.StreamVersionRowKey, cancellationToken: tokenSource.Token);

		streamVersion.ShouldNotBeNull();

		var streamVersionVersion = streamVersion![nameof(StreamVersionEntity.Version)] as int?;

		// Assert
		result.ShouldBeTrue();
		aggregate.IsNew().ShouldBeFalse();

		// Verify by re-getting the aggregate, knowing that the cache is disabled.
		var aggregateFromEventStore = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);

		aggregateFromEventStore.ShouldNotBeNull();
		aggregateFromEventStore.Id().ShouldBe(aggregate.Id());
		aggregateFromEventStore.IncrementInt32.ShouldBe(aggregate.IncrementInt32);
		aggregateFromEventStore.Details.SavedVersion.ShouldBe(aggregate.Details.SavedVersion);
		aggregateFromEventStore.Details.CurrentVersion.ShouldBe(aggregate.Details.CurrentVersion);
		aggregateFromEventStore.Details.SnapshotVersion.ShouldBe(aggregate.Details.SnapshotVersion);

		streamVersionVersion.ShouldBe(eventsToGenerate);
	}

	public async Task SaveAsync_GivenEventCountIsGreaterThanMaximumNumberOfAllowedEventsInSaveOperation_ThrowsException(int eventsToGenerate)
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToGenerate; i++)
			aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>();

		// Act
		var func = async () => await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		// Get and update stream version to remove the Version property.
		await Should.ThrowAsync<ArgumentOutOfRangeException>(func);
	}
}
