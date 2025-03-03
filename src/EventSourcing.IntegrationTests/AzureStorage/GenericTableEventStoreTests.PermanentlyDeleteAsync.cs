using System.Text;
using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);
		aggregate.ShouldNotBeNull();

		// Act
		var result = await eventStore.DeleteAsync(aggregate!, new EventStoreOperationContext
		{
			PermanentlyDelete = true
		}, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();

		aggregate.Details.IsDeleted.ShouldBeTrue();
		aggregate.Details.Locked.ShouldBeTrue();

		await ValidateEntitiesDeletedAsync(aggregate, eventStore, tokenSource.Token);
	}

	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

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

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

		await eventStore.SaveAsync(aggregate, cancellationToken: tokenSource.Token);

		aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: tokenSource.Token);
		aggregate.ShouldNotBeNull();

		// Act
		var result = await eventStore.DeleteAsync(aggregate!, new EventStoreOperationContext
		{
			PermanentlyDelete = true
		}, cancellationToken: tokenSource.Token);

		// Assert
		result.ShouldBeTrue();
		aggregate.Details.IsDeleted.ShouldBeTrue();
		aggregate.Details.Locked.ShouldBeTrue();

		await ValidateEntitiesDeletedAsync(aggregate, eventStore, tokenSource.Token);
	}

	async Task ValidateEntitiesDeletedAsync(TAggregate aggregate, TableEventStore<TAggregate> eventStore, CancellationToken cancellationToken)
	{
		var results = await fixture.TableClient.QueryAsync<TableEntity>(m => m.PartitionKey == aggregate.Details.Id, cancellationToken: cancellationToken);

		results.Results.ShouldBeEmpty();

		var prefix = eventStore.GenerateSnapshotBlobPath(aggregate.Id());
		var blobResults = await fixture.BlobClient.GetBlobsAsync(prefix, cancellationToken: cancellationToken);
		var blobsToDelete = blobResults.ToBlockingEnumerable(cancellationToken: cancellationToken);

		blobsToDelete.ShouldBeEmpty();
	}
}
