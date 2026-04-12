using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing;

public sealed class IEventStoreExtensionsEnlistTests
{
	[Test]
	public async Task Enlist_GivenNullEventStore_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		// Act & Assert
		await Assert.That(() => ((IEventStore<TestAggregate>)null!).Enlist(aggregate))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Enlist_GivenSingleAggregate_ReturnsPreparedTransaction(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		// Act
		await using var transaction = eventStore.Enlist(aggregate);
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await eventStore.Received(1).SaveAsync(
			aggregate,
			Arg.Any<EventStoreOperationContext?>(),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Enlist_GivenMultipleAggregates_EnlistsAllInTransaction(CancellationToken cancellationToken)
	{
		// Arrange
		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var a = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(a, new FluentValidation.Results.ValidationResult(), true, false);
			});

		// Act
		await using var transaction = eventStore.Enlist(agg1, agg2);
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert
		await Assert.That(result.Success).IsTrue();
		await Assert.That(result.Results).Count().IsEqualTo(2);
		await eventStore.Received(1).SaveAsync(agg1, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
		await eventStore.Received(1).SaveAsync(agg2, Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Enlist_WithCorrelationId_PropagatesCorrelationIdToTransaction(CancellationToken cancellationToken)
	{
		// Arrange
		var correlationId = "my-correlation-id";
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		// Act
		await using var transaction = eventStore.Enlist(correlationId, aggregate);
		await transaction.CommitAsync(cancellationToken);

		// Assert — the transaction uses the provided correlation ID
		await Assert.That(transaction.CorrelationId).IsEqualTo(correlationId);
		await eventStore.Received(1).SaveAsync(
			aggregate,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == correlationId),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Enlist_WithNullCorrelationId_GeneratesCorrelationId(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act
		await using var transaction = eventStore.Enlist(correlationId: null, aggregate);

		// Assert — a non-empty correlation ID was auto-generated
		await Assert.That(transaction.CorrelationId).IsNotEmpty();
		await Assert.That(Guid.TryParse(transaction.CorrelationId, out _)).IsTrue();
	}

	[Test]
	public async Task Enlist_WithOperationContext_UsesContextCorrelationIdForTransaction(CancellationToken cancellationToken)
	{
		// Arrange
		var correlationId = "context-correlation";
		var context = new EventStoreOperationContext { CorrelationId = correlationId };

		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(new SaveResult<TestAggregate>(aggregate, new FluentValidation.Results.ValidationResult(), true, false));

		// Act
		await using var transaction = eventStore.Enlist(context, aggregate);
		await transaction.CommitAsync(cancellationToken);

		// Assert — the transaction inherits the correlation ID from the context
		await Assert.That(transaction.CorrelationId).IsEqualTo(correlationId);
		await eventStore.Received(1).SaveAsync(
			aggregate,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == correlationId),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Enlist_WithNullOperationContext_GeneratesCorrelationId(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		aggregate.Increment();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act
		await using var transaction = eventStore.Enlist((EventStoreOperationContext?)null, aggregate);

		// Assert — auto-generated correlation ID
		await Assert.That(transaction.CorrelationId).IsNotEmpty();
	}

	[Test]
	public async Task Enlist_WithCorrelationIdAndNullAggregatesArray_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act & Assert
		await Assert.That(() => eventStore.Enlist(correlationId: "corr", aggregates: null!))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Enlist_WithOperationContextAndNullAggregatesArray_ThrowsArgumentNullException(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		var context = new EventStoreOperationContext();

		// Act & Assert
		await Assert.That(() => eventStore.Enlist(context, aggregates: null!))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Enlist_WithNoAggregates_ReturnsEmptyTransactionThatCommitsSuccessfully(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = Substitute.For<IEventStore<TestAggregate>>();

		// Act
		await using var transaction = eventStore.Enlist();
		var result = await transaction.CommitAsync(cancellationToken);

		// Assert — nothing to save, result has no entries but commits without error
		await Assert.That(result.Results).Count().IsEqualTo(0);
		await eventStore.DidNotReceive().SaveAsync(
			Arg.Any<TestAggregate>(),
			Arg.Any<EventStoreOperationContext?>(),
			Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Enlist_WithOperationContextAndMultipleAggregates_AppliesSameContextToAll(CancellationToken cancellationToken)
	{
		// Arrange
		var context = new EventStoreOperationContext { CorrelationId = "shared" };

		var agg1 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg1.Increment();

		var agg2 = TestHelpers.Aggregate<TestAggregate>(clearEvents: false);
		agg2.RecordEvent();

		var eventStore = Substitute.For<IEventStore<TestAggregate>>();
		eventStore
			.SaveAsync(Arg.Any<TestAggregate>(), Arg.Any<EventStoreOperationContext?>(), Arg.Any<CancellationToken>())
			.Returns(ci =>
			{
				var a = ci.ArgAt<TestAggregate>(0);
				return new SaveResult<TestAggregate>(a, new FluentValidation.Results.ValidationResult(), true, false);
			});

		// Act
		await using var transaction = eventStore.Enlist(context, agg1, agg2);
		await transaction.CommitAsync(cancellationToken);

		// Assert — both saves received the same context
		await eventStore.Received(1).SaveAsync(
			agg1,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == "shared"),
			Arg.Any<CancellationToken>()
		);
		await eventStore.Received(1).SaveAsync(
			agg2,
			Arg.Is<EventStoreOperationContext>(ctx => ctx.CorrelationId == "shared"),
			Arg.Any<CancellationToken>()
		);
	}
}
