using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.ChangeFeed;

namespace Purview.EventSourcing.AzureStorage;

partial class GenericTableEventStoreTests<TAggregate>
{
	public async Task SaveAsync_GivenAggregateWithChanges_NotifiesChangeFeed(
		int eventsToCreate,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();

		var beforeWasCalled = false;
		var afterWasCalled = false;
		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);
		for (var i = 0; i < eventsToCreate; i++)
			aggregate.AppendString($"{i + 1} of {eventsToCreate}(s) to created.");

		var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

		aggregateChangeNotifier
			.When(m => m.BeforeSaveAsync(Arg.Is(aggregate), Arg.Is(true), Arg.Any<CancellationToken>()))
			.Do(callInfo =>
			{
				var a = callInfo.ArgAt<TAggregate>(0);
				a.AppendString(nameof(aggregateChangeNotifier.AfterSaveAsync));

				beforeWasCalled = true;
			});

		aggregateChangeNotifier
			.When(m =>
				m.AfterSaveAsync(
					Arg.Is(aggregate),
					Arg.Is(0),
					Arg.Is(true),
					Arg.Any<IEvent[]>(),
					Arg.Any<CancellationToken>()
				)
			)
			.Do(_ => afterWasCalled = true);

		// Act
		bool result = await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(beforeWasCalled).IsTrue();
		await Assert.That(afterWasCalled).IsTrue();

		await aggregateChangeNotifier.Received(1).BeforeSaveAsync(aggregate, true, Arg.Any<CancellationToken>());

		await aggregateChangeNotifier
			.Received(1)
			.AfterSaveAsync(
				aggregate,
				0,
				true,
				Arg.Is<IEvent[]>(events => events.Length == eventsToCreate),
				Arg.Any<CancellationToken>()
			);
	}

	public async Task SaveAsync_GivenAggregateWithNoChanges_DoesNotNotifyChangeFeed(CancellationToken cancellationToken)
	{
		// Arrange
		var aggregateChangeNotifier = Substitute.For<IAggregateChangeFeedNotifier<TAggregate>>();

		var aggregateId = $"{Guid.NewGuid()}";
		var aggregate = TestHelpers.Aggregate<TAggregate>(aggregateId: aggregateId);

		var eventStore = fixture.CreateEventStore(aggregateChangeNotifier: aggregateChangeNotifier);

		// Act
		await eventStore.SaveAsync(aggregate, cancellationToken: cancellationToken);

		// Assert
		await aggregateChangeNotifier
			.DidNotReceive()
			.AfterSaveAsync(aggregate, 0, true, Arg.Any<IEvent[]>(), Arg.Any<CancellationToken>());
	}
}
