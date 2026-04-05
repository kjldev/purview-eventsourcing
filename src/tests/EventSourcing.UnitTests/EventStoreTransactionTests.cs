using NSubstitute.ExceptionExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing;

public sealed class EventStoreTransactionTests
{
	[Test]
	public async Task Enlist_GivenNullAggregate_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		await using var transaction = new EventStoreTransaction();
		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act & Assert
		await Assert.That(() => transaction.Enlist<TestAggregate>(null!, eventStore))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Enlist_GivenNullEventStore_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		await using var transaction = new EventStoreTransaction();
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		// Act & Assert
		await Assert.That(() => transaction.Enlist(aggregate, (IEventStore<TestAggregate>)null!))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task CommitAsync_GivenSingleAggregate_CallsSaveAsyncOnEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(
				Arg.Any<TestAggregate>(),
				Arg.Any<EventStoreOperationContext?>(),
				Arg.Any<CancellationToken>()
			)
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, eventStore);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsTrue();
		await Assert.That(result.Results[0].Skipped).IsFalse();
		await Assert.That(result.Results[0].Error).IsNull();
		await eventStore.Received(1).SaveAsync(
			aggregate,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == transaction.CorrelationId),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CommitAsync_GivenMultipleAggregates_SavesAllInOrder(CancellationToken cancellationToken)
	{
		// Arrange
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = Substitute.For<IEventStore<TestAggregate>>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, new FluentValidation.Results.ValidationResult(), true, false));

		var store2 = Substitute.For<IEventStore<TestAggregate>>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg2, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, store1);
		transaction.Enlist(agg2, store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Aggregate).IsEqualTo((IAggregate)agg1);
		await Assert.That(result.Results[1].Aggregate).IsEqualTo((IAggregate)agg2);
		await store1.Received(1).SaveAsync(agg1, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
		await store2.Received(1).SaveAsync(agg2, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CommitAsync_GivenSharedCorrelationId_PropagatesCorrelationIdToAllSaves(CancellationToken cancellationToken)
	{
		// Arrange
		var correlationId = "shared-correlation-123";
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = Substitute.For<IEventStore<TestAggregate>>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var agg = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(agg, new FluentValidation.Results.ValidationResult(), true, false);
			});

		var store2 = Substitute.For<IEventStore<TestAggregate>>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var agg = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(agg, new FluentValidation.Results.ValidationResult(), true, false);
			});

		await using var transaction = new EventStoreTransaction(correlationId);
		transaction.Enlist(agg1, store1);
		transaction.Enlist(agg2, store2);

		// Act
		await transaction.CommitAsync(cancellationToken);

		// Assert — both stores received the shared correlation ID
		await store1.Received(1).SaveAsync(
			agg1,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == correlationId),
			Arg.Any<CancellationToken>()
		);
		await store2.Received(1).SaveAsync(
			agg2,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == correlationId),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CommitAsync_WhenFirstAggregateFails_StopsAndDoesNotSaveSecond(CancellationToken cancellationToken)
	{
		// Arrange
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = Substitute.For<IEventStore<TestAggregate>>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("DB failure"));

		var store2 = Substitute.For<IEventStore<TestAggregate>>();

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, store1);
		transaction.Enlist(agg2, store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — failed on first, second never called
		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsFalse();
		await Assert.That(result.Results[0].Error).IsNotNull();
		await Assert.That(result.Results[0].Error).IsTypeOf<InvalidOperationException>();
		await store2.DidNotReceive().SaveAsync(
			Arg.Any<TestAggregate>(),
			Arg.Any<EventStoreOperationContext?>(),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CommitAsync_CalledTwice_ThrowsInvalidOperationException(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, eventStore);
		await transaction.CommitAsync(cancellationToken);

		// Act & Assert
		await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Enlist_AfterCommit_ThrowsInvalidOperationException(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, eventStore);
		await transaction.CommitAsync(cancellationToken);

		// Act & Assert
		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.Increment();
		await Assert.That(() => transaction.Enlist(agg2, eventStore))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Enlist_AfterDispose_ThrowsObjectDisposedException(CancellationToken cancellationToken)
	{
		// Arrange
		var transaction = new EventStoreTransaction();
		await transaction.DisposeAsync();

		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();
		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act & Assert
		await Assert.That(() => transaction.Enlist(aggregate, eventStore))
			.Throws<ObjectDisposedException>();
	}

	[Test]
	public async Task CommitAsync_AfterDispose_ThrowsObjectDisposedException(CancellationToken cancellationToken)
	{
		// Arrange
		var transaction = new EventStoreTransaction();
		await transaction.DisposeAsync();

		// Act & Assert
		await Assert.That(async () => await transaction.CommitAsync(cancellationToken))
			.Throws<ObjectDisposedException>();
	}

	[Test]
	public async Task CorrelationId_GivenExplicitValue_UsesProvidedValue(CancellationToken cancellationToken)
	{
		// Arrange
		var expectedCorrelationId = "my-custom-correlation-id";

		// Act
		await using var transaction = new EventStoreTransaction(expectedCorrelationId);

		// Assert
		await Assert.That(transaction.CorrelationId).IsEqualTo(expectedCorrelationId);
	}

	[Test]
	public async Task CorrelationId_GivenNull_GeneratesNewGuid(CancellationToken cancellationToken)
	{
		// Act
		await using var transaction = new EventStoreTransaction();

		// Assert — should be a valid GUID string
		await Assert.That(Guid.TryParse(transaction.CorrelationId, out _)).IsTrue();
	}

	[Test]
	public async Task CommitAsync_WhenSaveReturnsNotSaved_StopsAndReportsFailure(CancellationToken cancellationToken)
	{
		// Arrange — validation failure returns Saved=false, Skipped=false
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var validationResult = new FluentValidation.Results.ValidationResult(
			[new FluentValidation.Results.ValidationFailure("Field", "Required")]
		);

		var store1 = Substitute.For<IEventStore<TestAggregate>>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, validationResult, false, false));

		var store2 = Substitute.For<IEventStore<TestAggregate>>();

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, store1);
		transaction.Enlist(agg2, store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — first failed validation, second was never attempted
		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsFalse();
		await Assert.That(result.Results[0].Skipped).IsFalse();
		await store2.DidNotReceive().SaveAsync(
			Arg.Any<TestAggregate>(),
			Arg.Any<EventStoreOperationContext?>(),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CommitAsync_WhenSaveSkipped_ContinuesToNextAggregate(CancellationToken cancellationToken)
	{
		// Arrange — first aggregate has no unsaved events (skipped), second should still be saved
		var agg1 = TestHelpers.Aggregate<TestAggregate>(); // no events — cleared
		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.Increment();

		var store1 = Substitute.For<IEventStore<TestAggregate>>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, new FluentValidation.Results.ValidationResult(), false, true));

		var store2 = Substitute.For<IEventStore<TestAggregate>>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg2, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, store1);
		transaction.Enlist(agg2, store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — skipped first, saved second => overall not fully "success" but both were processed
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Skipped).IsTrue();
		await Assert.That(result.Results[1].Saved).IsTrue();
		await store2.Received(1).SaveAsync(
			agg2,
			Arg.Any<EventStoreOperationContext?>(),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CommitAsync_GivenCustomOperationContext_PreservesExistingCorrelationId(CancellationToken cancellationToken)
	{
		// Arrange — user provides a custom context with an already-set correlation ID
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var customCorrelation = "user-provided-correlation";
		var customContext = new EventStoreOperationContext { CorrelationId = customCorrelation };

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction("transaction-correlation");
		transaction.Enlist(aggregate, eventStore, customContext);

		// Act
		await transaction.CommitAsync(cancellationToken);

		// Assert — the user-provided correlation ID is preserved (not overwritten by the transaction)
		await eventStore.Received(1).SaveAsync(
			aggregate,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == customCorrelation),
			Arg.Any<CancellationToken>()
		);
	}
}
