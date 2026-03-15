using System.Text;
using Azure.Data.Tables;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);
		var tableClient = fixture.TableClient;
		var blobClient = fixture.BlobClient;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);
		await Assert.That(aggregate).IsNotNull();

		// Act
		var result = await eventStore.DeleteAsync(
			aggregate!,
			new EventStoreOperationContext { PermanentlyDelete = true },
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsTrue();

		await Assert.That(aggregate.Details.IsDeleted).IsTrue();
		await Assert.That(aggregate.Details.Locked).IsTrue();

		await ValidateEntitiesDeletedAsync(aggregate, eventStore, tableClient, blobClient, cancellationToken);
	}

	public async Task DeleteAsync_GivenAggregateExistsWithLargeEvent_PermanentlyDeletesAllData(CancellationToken cancellationToken)
	{
		// Arrange
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
		var tableClient = fixture.TableClient;
		var blobClient = fixture.BlobClient;

		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		aggregate = await eventStore.GetAsync(aggregateId, cancellationToken: cancellationToken);
		await Assert.That(aggregate).IsNotNull();

		// Act
		var result = await eventStore.DeleteAsync(
			aggregate!,
			new EventStoreOperationContext { PermanentlyDelete = true },
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result).IsTrue();
		await Assert.That(aggregate.Details.IsDeleted).IsTrue();
		await Assert.That(aggregate.Details.Locked).IsTrue();

		await ValidateEntitiesDeletedAsync(aggregate, eventStore, tableClient, blobClient, cancellationToken);
	}

	async Task ValidateEntitiesDeletedAsync(
		TAggregate aggregate,
		TableEventStore<TAggregate> eventStore,
		AzureTableClient tableClient,
		AzureBlobClient blobClient,
		CancellationToken cancellationToken
	)
	{
		var results = await tableClient.QueryAsync<TableEntity>(
			m => m.PartitionKey == aggregate.Details.Id,
			cancellationToken: cancellationToken
		);

		await Assert.That(results.Results).IsEmpty();

		var prefix = eventStore.GenerateSnapshotBlobPath(aggregate.Id());
		var blobResults = await blobClient.GetBlobsAsync(prefix, cancellationToken: cancellationToken);
		var blobsToDelete = blobResults.ToBlockingEnumerable(cancellationToken: cancellationToken);

		await Assert.That(blobsToDelete).IsEmpty();
	}
}
