using System.Text;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class GenericMongoDBEventStoreTests<TAggregate>
{
	public async Task DeleteAsync_GivenAggregateExists_PermanentlyDeletesAllData()
	{
		// Arrange
		using var tokenSource = TestHelpers.CancellationTokenSource(cancellationToken: TestContext.Current.CancellationToken);

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

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

		await ValidateEntitiesDeletedAsync(aggregate, tokenSource.Token);
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

		using var eventStore = fixture.CreateEventStore<TAggregate>(correlationIdsToGenerate: 2);

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

		await ValidateEntitiesDeletedAsync(aggregate, tokenSource.Token);
	}

	async Task ValidateEntitiesDeletedAsync(TAggregate aggregate, CancellationToken cancellationToken)
	{
		var eventCount = await fixture.EventClient.CountAsync<EventEntity>(m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.EventType, cancellationToken: cancellationToken);
		eventCount.ShouldBe(0);

		var streamVersionCount = await fixture.EventClient.CountAsync<StreamVersionEntity>(m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.StreamVersionType, cancellationToken: cancellationToken);
		streamVersionCount.ShouldBe(0);

		var idempotencyMarkerCount = await fixture.EventClient.CountAsync<IdempotencyMarkerEntity>(m => m.AggregateId == aggregate.Id() && m.EntityType == EntityTypes.StreamVersionType, cancellationToken: cancellationToken);
		idempotencyMarkerCount.ShouldBe(0);

		var snapshotEntity = await fixture.SnapshotClient.GetAsync<SnapshotEntity>(m => m.Id == aggregate.Id() && m.EntityType == EntityTypes.SnapshotType, cancellationToken: cancellationToken);
		snapshotEntity.ShouldBeNull();
	}
}
