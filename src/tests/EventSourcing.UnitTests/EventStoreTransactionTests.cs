using System.Data;
using System.Data.Common;
using NSubstitute.ExceptionExtensions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Aggregates.Test;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing;

public sealed class EventStoreTransactionTests
{
	[Test]
	public async Task Enlist_GivenNullAggregate_ThrowsArgumentNullException()
	{
		// Arrange
		await using var transaction = new EventStoreTransaction();
		var eventStore = Substitute.For<IEventStore>();

		// Act & Assert
		await Assert.That(() => transaction.Enlist<TestAggregate>(null!, eventStore)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Enlist_GivenNullEventStore_ThrowsArgumentNullException()
	{
		// Arrange
		await using var transaction = new EventStoreTransaction();
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		// Act & Assert
		await Assert.That(() => transaction.Enlist(aggregate, (IEventStore)null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task CommitAsync_GivenSingleAggregate_CallsSaveAsyncOnEventStore(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(
				new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false)
			);

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, (IEventStore)eventStore);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsTrue();
		await Assert.That(result.Results[0].Skipped).IsFalse();
		await Assert.That(result.Results[0].Error).IsNull();
		await eventStore
			.Received(1)
			.SaveAsync(
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

		var store1 = Substitute.For<IEventStore>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, new FluentValidation.Results.ValidationResult(), true, false));

		var store2 = Substitute.For<IEventStore>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg2, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Aggregate).IsEqualTo(agg1);
		await Assert.That(result.Results[1].Aggregate).IsEqualTo(agg2);
		await store1.Received(1).SaveAsync(agg1, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
		await store2.Received(1).SaveAsync(agg2, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CommitAsync_GivenSharedCorrelationId_PropagatesCorrelationIdToAllSaves(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var correlationId = "shared-correlation-123";
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = Substitute.For<IEventStore>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var agg = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(agg, new FluentValidation.Results.ValidationResult(), true, false);
			});

		var store2 = Substitute.For<IEventStore>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var agg = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(agg, new FluentValidation.Results.ValidationResult(), true, false);
			});

		await using var transaction = new EventStoreTransaction(correlationId);
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		// Act
		await transaction.CommitAsync(cancellationToken);

		// Assert — both stores received the shared correlation ID
		await store1
			.Received(1)
			.SaveAsync(
				agg1,
				Arg.Is<EventStoreOperationContext>(ctx =>
					ctx.CorrelationId == correlationId && ctx.UseIdempotencyMarker
				),
				Arg.Any<CancellationToken>()
			);
		await store2
			.Received(1)
			.SaveAsync(
				agg2,
				Arg.Is<EventStoreOperationContext>(ctx =>
					ctx.CorrelationId == correlationId && ctx.UseIdempotencyMarker
				),
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

		var store1 = Substitute.For<IEventStore>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("DB failure"));

		var store2 = Substitute.For<IEventStore>();

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — failed on first, second never called
		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsFalse();
		await Assert.That(result.Results[0].Error).IsNotNull();
		await Assert.That(result.Results[0].Error).IsTypeOf<InvalidOperationException>();
		await store2
			.DidNotReceive()
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CommitAsync_CalledTwice_ThrowsInvalidOperationException(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(
				new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false)
			);

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, eventStore);
		await transaction.CommitAsync(cancellationToken);

		// Act & Assert
		await Assert
			.That(async () => await transaction.CommitAsync(cancellationToken))
			.Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Enlist_AfterCommit_ThrowsInvalidOperationException(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(
				new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false)
			);

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(aggregate, eventStore);
		await transaction.CommitAsync(cancellationToken);

		// Act & Assert
		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.Increment();
		await Assert.That(() => transaction.Enlist(agg2, eventStore)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Enlist_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var transaction = new EventStoreTransaction();
		await transaction.DisposeAsync();

		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();
		var eventStore = Substitute.For<IEventStore>();

		// Act & Assert
		await Assert.That(() => transaction.Enlist(aggregate, eventStore)).Throws<ObjectDisposedException>();
	}

	[Test]
	public async Task CommitAsync_AfterDispose_ThrowsObjectDisposedException(CancellationToken cancellationToken)
	{
		// Arrange
		var transaction = new EventStoreTransaction();
		await transaction.DisposeAsync();

		// Act & Assert
		await Assert
			.That(async () => await transaction.CommitAsync(cancellationToken))
			.Throws<ObjectDisposedException>();
	}

	[Test]
	public async Task CommitAsync_WhenAggregatesShareTransactionalBoundary_UsesNativeTransactionCoordinator(
		CancellationToken cancellationToken
	)
	{
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = new FakeTransactionalEventStore("sqlserver:primary");
		var store2 = new FakeTransactionalEventStore("sqlserver:primary");

		await using var transaction = new EventStoreTransaction("coordinated");
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsTrue();
		await Assert.That(store1.SaveAsyncCalls).IsEqualTo(0);
		await Assert.That(store2.SaveAsyncCalls).IsEqualTo(0);
		await Assert.That(store1.SaveInTransactionCalls).IsEqualTo(1);
		await Assert.That(store2.SaveInTransactionCalls).IsEqualTo(1);
		await Assert.That(store1.EnsureConfiguredCalls).IsEqualTo(1);
		await Assert.That(store2.EnsureConfiguredCalls).IsEqualTo(1);
		await Assert.That(store1.AfterCommitCalls).IsEqualTo(1);
		await Assert.That(store2.AfterCommitCalls).IsEqualTo(1);
		await Assert.That(store1.LastCorrelationId).IsEqualTo("coordinated");
		await Assert.That(store2.LastCorrelationId).IsEqualTo("coordinated");
		await Assert.That(store1.LastUseIdempotencyMarker).IsTrue();
		await Assert.That(store2.LastUseIdempotencyMarker).IsTrue();
	}

	[Test]
	public async Task CommitAsync_WhenTransactionalBoundariesDiffer_FallsBackToSequentialCoordinator(
		CancellationToken cancellationToken
	)
	{
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var store1 = new FakeTransactionalEventStore("sqlserver:primary");
		var store2 = new FakeTransactionalEventStore("sqlserver:secondary");

		await using var transaction = new EventStoreTransaction("fallback");
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsTrue();
		await Assert.That(store1.SaveAsyncCalls).IsEqualTo(1);
		await Assert.That(store2.SaveAsyncCalls).IsEqualTo(1);
		await Assert.That(store1.SaveInTransactionCalls).IsEqualTo(0);
		await Assert.That(store2.SaveInTransactionCalls).IsEqualTo(0);
		await Assert.That(store1.EnsureConfiguredCalls).IsEqualTo(0);
		await Assert.That(store2.EnsureConfiguredCalls).IsEqualTo(0);
		await Assert.That(store1.LastCorrelationId).IsEqualTo("fallback");
		await Assert.That(store2.LastCorrelationId).IsEqualTo("fallback");
		await Assert.That(store1.LastUseIdempotencyMarker).IsTrue();
		await Assert.That(store2.LastUseIdempotencyMarker).IsTrue();
	}

	[Test]
	public async Task CommitAsync_WhenAnyStoreLacksNativeTransactions_FallsBackToSequentialCoordinator(
		CancellationToken cancellationToken
	)
	{
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var transactionalStore = new FakeTransactionalEventStore("sqlserver:primary");
		var nonTransactionalStore = Substitute.For<IEventStore>();
		nonTransactionalStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var aggregate = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(
					aggregate,
					new FluentValidation.Results.ValidationResult(),
					true,
					false
				);
			});

		await using var transaction = new EventStoreTransaction("mixed");
		transaction.Enlist(agg1, (IEventStore)transactionalStore);
		transaction.Enlist(agg2, nonTransactionalStore);

		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsTrue();
		await Assert.That(transactionalStore.SaveAsyncCalls).IsEqualTo(1);
		await Assert.That(transactionalStore.SaveInTransactionCalls).IsEqualTo(0);
		await nonTransactionalStore
			.Received(1)
			.SaveAsync(
				agg2,
				Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == "mixed"),
				Arg.Any<CancellationToken>()
			);
	}

	[Test]
	public async Task CommitAsync_WhenNativeTransactionFails_RollsBackProcessedAggregates(
		CancellationToken cancellationToken
	)
	{
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var successfulStore = new FakeTransactionalEventStore("sqlserver:primary");
		var failingStore = new FakeTransactionalEventStore(
			"sqlserver:primary",
			new SaveBehavior(false, false, new InvalidOperationException("boom"))
		);

		await using var transaction = new EventStoreTransaction("rollback");
		transaction.Enlist(agg1, (IEventStore)successfulStore);
		transaction.Enlist(agg2, (IEventStore)failingStore);

		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(successfulStore.AfterCommitCalls).IsEqualTo(0);
		await Assert.That(successfulStore.AfterRollbackCalls).IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsFalse();
		await Assert.That(result.Results[0].Error).IsNotNull();
		await Assert.That(result.Results[1].Error).IsTypeOf<InvalidOperationException>();
	}

	[Test]
	public async Task CorrelationId_GivenExplicitValue_UsesProvidedValue()
	{
		// Arrange
		var expectedCorrelationId = "my-custom-correlation-id";

		// Act
		await using var transaction = new EventStoreTransaction(expectedCorrelationId);

		// Assert
		await Assert.That(transaction.CorrelationId).IsEqualTo(expectedCorrelationId);
	}

	[Test]
	public async Task CorrelationId_GivenNull_GeneratesNewGuid()
	{
		var currentActivity = System.Diagnostics.Activity.Current;
		System.Diagnostics.Activity.Current = null;
		try
		{
			// Act
			await using var transaction = new EventStoreTransaction();

			// Assert — should be a valid GUID string
			await Assert.That(Guid.TryParse(transaction.CorrelationId, out _)).IsTrue();
		}
		finally
		{
			System.Diagnostics.Activity.Current = currentActivity;
		}
	}

	[Test]
	public async Task CorrelationId_GivenCurrentActivity_UsesActivityId()
	{
		// Arrange
		using var activity = new System.Diagnostics.Activity("event-store-transaction");
		activity.Start();

		// Act
		await using var transaction = new EventStoreTransaction();

		// Assert
		await Assert.That(transaction.CorrelationId).IsEqualTo(activity.Id);
	}

	[Test]
	public async Task CommitAsync_WhenSaveReturnsNotSaved_StopsAndReportsFailure(CancellationToken cancellationToken)
	{
		// Arrange — validation failure returns Saved=false, Skipped=false
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var validationResult = new FluentValidation.Results.ValidationResult([
			new FluentValidation.Results.ValidationFailure("Field", "Required"),
		]);

		var store1 = Substitute.For<IEventStore>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, validationResult, false, false));

		var store2 = Substitute.For<IEventStore>();

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — first failed validation, second was never attempted
		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Saved).IsFalse();
		await Assert.That(result.Results[0].Skipped).IsFalse();
		await store2
			.DidNotReceive()
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CommitAsync_WhenSaveSkipped_ContinuesToNextAggregate(CancellationToken cancellationToken)
	{
		// Arrange — first aggregate has no unsaved events (skipped), second should still be saved
		var agg1 = TestHelpers.Aggregate<TestAggregate>(); // no events — cleared
		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.Increment();

		var store1 = Substitute.For<IEventStore>();
		store1
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg1, new FluentValidation.Results.ValidationResult(), false, true));

		var store2 = Substitute.For<IEventStore>();
		store2
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg2, new FluentValidation.Results.ValidationResult(), true, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg1, (IEventStore)store1);
		transaction.Enlist(agg2, (IEventStore)store2);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — skipped first, saved second => overall not fully "success" but both were processed
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Skipped).IsTrue();
		await Assert.That(result.Results[1].Saved).IsTrue();
		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.CompletedWithoutError).IsTrue();
		await store2.Received(1).SaveAsync(agg2, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task CommitAsync_GivenCustomOperationContext_ClonesContextRatherThanMutatingShared(
		CancellationToken cancellationToken
	)
	{
		// Arrange — no custom context, so DefaultContext is cloned
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(
				new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false)
			);

		var originalDefaultCorrelationId = EventStoreOperationContext.DefaultContext().CorrelationId;

		await using var transaction = new EventStoreTransaction("my-correlation");
		transaction.Enlist(aggregate, eventStore);

		// Act
		await transaction.CommitAsync(cancellationToken);

		// Assert — DefaultContext was not mutated
		await Assert
			.That(EventStoreOperationContext.DefaultContext().CorrelationId)
			.IsEqualTo(originalDefaultCorrelationId);
	}

	[Test]
	public async Task TransactionResult_CompletedWithoutError_ReturnsTrueForEmptyTransaction(
		CancellationToken cancellationToken
	)
	{
		// An empty transaction has no failures, so CompletedWithoutError should be true
		await using var transaction = new EventStoreTransaction();
		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.CompletedWithoutError).IsTrue();
	}

	[Test]
	public async Task TransactionResult_CompletedWithoutError_ReturnsFalseWhenAggregateFailedToSave(
		CancellationToken cancellationToken
	)
	{
		// Arrange — first aggregate save returns Saved=false, Skipped=false (failure)
		var agg = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg.Increment();

		var store = Substitute.For<IEventStore>();
		store
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(agg, new FluentValidation.Results.ValidationResult(), false, false));

		await using var transaction = new EventStoreTransaction();
		transaction.Enlist(agg, (IEventStore)store);

		// Act
		var result = await transaction.CommitAsync(cancellationToken);

		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.CompletedWithoutError).IsFalse();
	}

	[Test]
	public async Task CommitAsync_GivenCustomOperationContext_PreservesExistingCorrelationId(
		CancellationToken cancellationToken
	)
	{
		// Arrange — user provides a custom context with an already-set correlation ID
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var customCorrelation = "user-provided-correlation";
		var customContext = new EventStoreOperationContext { CorrelationId = customCorrelation };

		var eventStore = Substitute.For<IEventStore>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(
				new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false)
			);

		await using var transaction = new EventStoreTransaction("transaction-correlation");
		transaction.Enlist(aggregate, (IEventStore)eventStore, customContext);

		// Act
		await transaction.CommitAsync(cancellationToken);

		// Assert — the user-provided correlation ID is preserved (not overwritten by the transaction)
		await eventStore
			.Received(1)
			.SaveAsync(
				aggregate,
				Arg.Is<EventStoreOperationContext>(ctx =>
					ctx.CorrelationId == customCorrelation && ctx.UseIdempotencyMarker
				),
				Arg.Any<CancellationToken>()
			);
	}

	[Test]
	public async Task CommitAsync_GivenGeneratedCorrelationId_DoesNotForceIdempotencyMarker(
		CancellationToken cancellationToken
	)
	{
		var currentActivity = System.Diagnostics.Activity.Current;
		System.Diagnostics.Activity.Current = null;
		try
		{
			// Arrange
			var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
			aggregate.Increment();

			var eventStore = new FakeTransactionalEventStore("sqlserver:primary");

			// Act
			await using var transaction = new EventStoreTransaction();
			transaction.Enlist(aggregate, (IEventStore)eventStore);
			await transaction.CommitAsync(cancellationToken);

			// Assert
			await Assert.That(eventStore.LastUseIdempotencyMarker).IsFalse();
		}
		finally
		{
			System.Diagnostics.Activity.Current = currentActivity;
		}
	}

	sealed record SaveBehavior(bool Saved, bool Skipped, Exception? Exception = null);

	sealed class FakeTransactionalEventStore(string transactionBoundaryKey, SaveBehavior? behavior = null)
		: ITransactionalEventStore<TestAggregate>,
			IEventStore,
			IEventStoreImplementationAccessor
	{
		readonly SaveBehavior _behavior = behavior ?? new SaveBehavior(true, false);

		public int SaveAsyncCalls { get; private set; }

		public int SaveInTransactionCalls { get; private set; }

		public int EnsureConfiguredCalls { get; private set; }

		public int AfterCommitCalls { get; private set; }

		public int AfterRollbackCalls { get; private set; }

		public string? LastCorrelationId { get; private set; }

		public bool? LastUseIdempotencyMarker { get; private set; }

		public string TransactionBoundaryKey { get; } = transactionBoundaryKey;

		public DbConnection CreateTransactionConnection() => new FakeDbConnection();

		public Task EnsureTransactionConfiguredAsync(DbConnection connection, CancellationToken _ = default)
		{
			EnsureConfiguredCalls++;
			return Task.CompletedTask;
		}

		public Task<TransactionalSaveOperation<TestAggregate>> SaveInTransactionAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			DbConnection connection,
			DbTransaction transaction,
			CancellationToken _ = default
		)
		{
			SaveInTransactionCalls++;
			LastCorrelationId = operationContext?.CorrelationId;
			LastUseIdempotencyMarker = operationContext?.UseIdempotencyMarker;

			return _behavior.Exception is not null
				? throw _behavior.Exception
				: Task.FromResult(CreateOperation(aggregate));
		}

		public Task<SaveResult<TestAggregate>> SaveAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		)
		{
			SaveAsyncCalls++;
			LastCorrelationId = operationContext?.CorrelationId;
			LastUseIdempotencyMarker = operationContext?.UseIdempotencyMarker;

			return _behavior.Exception is not null
				? throw _behavior.Exception
				: Task.FromResult(CreateOperation(aggregate).Result);
		}

		Task<SaveResult<T>> IEventStore.SaveAsync<T>(
			T aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken
		)
		{
			return aggregate is TestAggregate testAggregate && typeof(T) == typeof(TestAggregate)
				? (Task<SaveResult<T>>)(object)SaveAsync(testAggregate, operationContext, cancellationToken)
				: throw new NotSupportedException();
		}

		IEventStoreCore<T> IEventStoreImplementationAccessor.GetEventStore<T>()
		{
			return typeof(T) == typeof(TestAggregate)
				? (IEventStoreCore<T>)(object)this
				: throw new NotSupportedException();
		}

		Task<T> IEventStore.CreateAsync<T>(string? aggregateId, CancellationToken _) =>
			throw new NotSupportedException();

		Task<T?> IEventStore.GetOrCreateAsync<T>(
			string? aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken _
		)
			where T : class => throw new NotSupportedException();

		Task<T?> IEventStore.GetAsync<T>(
			string aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken _
		)
			where T : class => throw new NotSupportedException();

		Task<T?> IEventStore.GetAtAsync<T>(
			string aggregateId,
			int version,
			EventStoreOperationContext? operationContext,
			CancellationToken _
		)
			where T : class => throw new NotSupportedException();

		Task<bool> IEventStore.IsDeletedAsync<T>(string aggregateId, CancellationToken _) =>
			throw new NotSupportedException();

		Task<T?> IEventStore.GetDeletedAsync<T>(string aggregateId, CancellationToken _)
			where T : class => throw new NotSupportedException();

		Task<bool> IEventStore.DeleteAsync<T>(
			T aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken _
		) => throw new NotSupportedException();

		Task<bool> IEventStore.RestoreAsync<T>(
			T aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken _
		) => throw new NotSupportedException();

		async IAsyncEnumerable<string> IEventStore.GetAggregateIdsAsync<T>(
			bool includeDeleted,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken _
		)
		{
			await Task.CompletedTask;
			yield break;
		}

		Task<ExistsState> IEventStore.ExistsAsync<T>(string aggregateId, CancellationToken _) =>
			throw new NotSupportedException();

		T IEventStore.FulfilRequirements<T>(T aggregate) => aggregate;

		IAsyncEnumerable<(IEvent @event, string eventType)> IEventStore.GetEventRangeAsync<T>(
			string aggregateId,
			int versionFrom,
			int? versionTo,
			CancellationToken cancellationToken
		) => throw new NotSupportedException();

		TransactionalSaveOperation<TestAggregate> CreateOperation(TestAggregate aggregate) =>
			new(
				new SaveResult<TestAggregate>(
					aggregate,
					new FluentValidation.Results.ValidationResult(),
					_behavior.Saved,
					_behavior.Skipped
				),
				_ =>
				{
					AfterCommitCalls++;
					return Task.CompletedTask;
				},
				_ =>
				{
					AfterRollbackCalls++;
					return Task.CompletedTask;
				}
			);

		public Task<TestAggregate> CreateAsync(string? aggregateId = null, CancellationToken _ = default) =>
			throw new NotSupportedException();

		public Task<TestAggregate?> GetOrCreateAsync(
			string? aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		) => throw new NotSupportedException();

		public Task<TestAggregate?> GetAsync(
			string aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		) => throw new NotSupportedException();

		public Task<TestAggregate?> GetAtAsync(
			string aggregateId,
			int version,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		) => throw new NotSupportedException();

		public Task<bool> IsDeletedAsync(string aggregateId, CancellationToken _ = default) =>
			throw new NotSupportedException();

		public Task<TestAggregate?> GetDeletedAsync(string aggregateId, CancellationToken _ = default) =>
			throw new NotSupportedException();

		public Task<bool> DeleteAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		) => throw new NotSupportedException();

		public Task<bool> RestoreAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken _ = default
		) => throw new NotSupportedException();

		public async IAsyncEnumerable<string> GetAggregateIdsAsync(
			bool includeDeleted,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken _ = default
		)
		{
			yield break;
		}

		public Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken _ = default) =>
			throw new NotSupportedException();

		public TestAggregate FulfilRequirements(TestAggregate aggregate) => aggregate;

		public IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
			string aggregateId,
			int versionFrom,
			int? versionTo,
			CancellationToken cancellationToken = default
		) => throw new NotSupportedException();
	}

	sealed class FakeDbConnection : DbConnection
	{
		ConnectionState _state = ConnectionState.Closed;

		[System.Diagnostics.CodeAnalysis.AllowNull]
		public override string ConnectionString { get; set; } = "fake";
		public override string Database => "fake";
		public override string DataSource => "fake";
		public override string ServerVersion => "1.0";
		public override ConnectionState State => _state;

		public override void ChangeDatabase(string databaseName) { }

		public override void Close() => _state = ConnectionState.Closed;

		public override void Open() => _state = ConnectionState.Open;

		public override Task OpenAsync(CancellationToken _)
		{
			Open();
			return Task.CompletedTask;
		}

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
			new FakeDbTransaction(this);

		protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
	}

	sealed class FakeDbTransaction(FakeDbConnection connection) : DbTransaction
	{
		public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

		protected override DbConnection DbConnection => connection;

		public override void Commit() { }

		public override void Rollback() { }

		public override Task CommitAsync(CancellationToken _ = default) => Task.CompletedTask;

		public override Task RollbackAsync(CancellationToken _ = default) => Task.CompletedTask;
	}
}
