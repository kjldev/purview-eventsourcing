using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.ChangeFeed;

namespace Purview.EventSourcing.SqlServer;

partial class GenericSqlServerEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(int eventsToCreate, CancellationToken cancellationToken)
	{
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();
		var beforeWasCalled = false;
		var afterWasCalled = false;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.IncrementInt32Value();

		using var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

		aggregateChangeNotifier
			.When(m => m.BeforeSaveAsync(aggregate, true, Arg.Any<CancellationToken>()))
			.Do(_ => beforeWasCalled = true);
		aggregateChangeNotifier
			.When(m =>
				m.AfterSaveAsync(
					aggregate,
					Arg.Any<int>(),
					true,
					Arg.Any<IEvent[]>(),
					Arg.Any<CancellationToken>()
				)
			)
			.Do(_ => afterWasCalled = true);

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result.Saved).IsTrue();
		await Assert.That(beforeWasCalled).IsTrue();
		await Assert.That(afterWasCalled).IsTrue();

		await aggregateChangeNotifier.Received(1).BeforeSaveAsync(aggregate, true, Arg.Any<CancellationToken>());
		await aggregateChangeNotifier
			.Received(1)
			.AfterSaveAsync(aggregate, Arg.Any<int>(), true, Arg.Any<IEvent[]>(), Arg.Any<CancellationToken>());
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(CancellationToken cancellationToken)
	{
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		using var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

		var result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		await Assert.That(result.Saved).IsFalse();

		await aggregateChangeNotifier
			.DidNotReceive()
			.BeforeSaveAsync(Arg.Any<TAggregate>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		await aggregateChangeNotifier
			.DidNotReceive()
			.AfterSaveAsync(Arg.Any<TAggregate>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<IEvent[]>(), Arg.Any<CancellationToken>());
	}
}
