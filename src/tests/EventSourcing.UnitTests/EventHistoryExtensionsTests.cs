using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing;

public sealed class EventHistoryExtensionsTests
{
	[Test]
	public async Task GetEventHistoryAsync_GivenVersionAndTimeFilters_ReturnsMatchingEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
		HistoryEnabledStore store = new([
			CreateEvent("Created", 1, baseTime),
			CreateEvent("Updated", 2, baseTime.AddMinutes(1)),
			CreateEvent("Updated", 3, baseTime.AddMinutes(2)),
			CreateEvent("Deleted", 4, baseTime.AddMinutes(3)),
		]);

		AggregateEventHistoryRequest request = new()
		{
			FromVersion = 2,
			ToVersion = 4,
			FromUtc = baseTime.AddMinutes(1),
			ToUtc = baseTime.AddMinutes(2),
			MaxRecords = 10,
		};

		// Act
		var response = await store.GetEventHistoryAsync("agg-1", request, cancellationToken);

		// Assert
		await Assert.That(response.Results).Count().IsEqualTo(2);
		await Assert.That(response.Results.Select(m => m.AggregateVersion)).IsEquivalentTo([2, 3]);
		await Assert.That(response.ContinuationToken).IsNull();
	}

	[Test]
	public async Task GetEventHistoryAsync_GivenContinuationToken_PaginatesResults(CancellationToken cancellationToken)
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
		HistoryEnabledStore store = new([
			CreateEvent("EventA", 1, baseTime),
			CreateEvent("EventB", 2, baseTime.AddMinutes(1)),
			CreateEvent("EventC", 3, baseTime.AddMinutes(2)),
		]);

		var first = await store.GetEventHistoryAsync(
			"agg-1",
			new AggregateEventHistoryRequest { MaxRecords = 2 },
			cancellationToken
		);

		// Act
		var second = await store.GetEventHistoryAsync(
			"agg-1",
			new AggregateEventHistoryRequest { MaxRecords = 2, ContinuationToken = first.ContinuationToken },
			cancellationToken
		);

		// Assert
		await Assert.That(first.Results).Count().IsEqualTo(2);
		await Assert.That(first.ContinuationToken).IsEqualTo("k2");
		await Assert.That(second.Results).Count().IsEqualTo(1);
		await Assert.That(second.Results[0].AggregateVersion).IsEqualTo(3);
		await Assert.That(second.ContinuationToken).IsNull();
	}

	[Test]
	public async Task GetEventHistoryAsync_ExposesCompletePersistedMetadata(CancellationToken cancellationToken)
	{
		var timestamp = DateTimeOffset.UtcNow;
		TestAuditEvent sourceEvent = new()
		{
			Details = new EventDetails
			{
				AggregateVersion = 7,
				SchemaVersion = 3,
				When = timestamp,
				IdempotencyId = "idempotency-7",
				CorrelationId = "correlation-7",
				CausationId = "causation-6",
				UserId = "user-7",
			},
		};
		HistoryEnabledStore store = new([(sourceEvent, "Updated")]);

		var response = await store.GetEventHistoryAsync("agg-1", cancellationToken: cancellationToken);

		var item = response.Results.Single();
		await Assert.That(item.SchemaVersion).IsEqualTo(3);
		await Assert.That(item.AggregateVersion).IsEqualTo(7);
		await Assert.That(item.When).IsEqualTo(timestamp);
		await Assert.That(item.IdempotencyId).IsEqualTo("idempotency-7");
		await Assert.That(item.CorrelationId).IsEqualTo("correlation-7");
		await Assert.That(item.CausationId).IsEqualTo("causation-6");
		await Assert.That(item.UserId).IsEqualTo("user-7");
	}

	[Test]
	public async Task GetEventHistoryAsync_GivenLegacyOffsetToken_StillPaginates(CancellationToken cancellationToken)
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
		HistoryEnabledStore store = new([
			CreateEvent("EventA", 1, baseTime),
			CreateEvent("EventB", 2, baseTime.AddMinutes(1)),
			CreateEvent("EventC", 3, baseTime.AddMinutes(2)),
		]);

		// A legacy integer token (produced by earlier versions) must skip matched records.
		var second = await store.GetEventHistoryAsync(
			"agg-1",
			new AggregateEventHistoryRequest { MaxRecords = 2, ContinuationToken = "2" },
			cancellationToken
		);

		// Assert
		await Assert.That(second.Results).Count().IsEqualTo(1);
		await Assert.That(second.Results[0].AggregateVersion).IsEqualTo(3);
		await Assert.That(second.ContinuationToken).IsNull();
	}

	[Test]
	public async Task GetEventHistoryAsync_GivenKeysetTokenWithTimeFilter_PaginatesCorrectly(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);
		HistoryEnabledStore store = new([
			CreateEvent("EventA", 1, baseTime),
			CreateEvent("EventB", 2, baseTime.AddMinutes(1)),
			CreateEvent("EventC", 3, baseTime.AddMinutes(2)),
			CreateEvent("EventD", 4, baseTime.AddMinutes(3)),
		]);

		// Page 1 returns versions 1-2 (time filter keeps everything).
		var first = await store.GetEventHistoryAsync(
			"agg-1",
			new AggregateEventHistoryRequest
			{
				MaxRecords = 2,
				FromUtc = baseTime,
				ToUtc = baseTime.AddMinutes(3),
			},
			cancellationToken
		);

		var second = await store.GetEventHistoryAsync(
			"agg-1",
			new AggregateEventHistoryRequest
			{
				MaxRecords = 2,
				FromUtc = baseTime,
				ToUtc = baseTime.AddMinutes(3),
				ContinuationToken = first.ContinuationToken,
			},
			cancellationToken
		);

		// Assert
		await Assert.That(first.Results).Count().IsEqualTo(2);
		await Assert.That(first.ContinuationToken).IsEqualTo("k2");
		await Assert.That(second.Results).Count().IsEqualTo(2);
		await Assert.That(second.Results.Select(m => m.AggregateVersion)).IsEquivalentTo([3, 4]);
		await Assert.That(second.ContinuationToken).IsNull();
	}

	[Test]
	public async Task GetEventHistoryAsync_GivenStoreWithoutHistorySupport_ThrowsNotSupportedException()
	{
		// Arrange
		var store = IEventStoreCore<TestAggregate>.Mock();

		// Act
		var exception = (
			await Assert.That(() => store.GetEventHistoryAsync("agg-1")!).Throws<NotSupportedException>()
		)!;

		// Assert
		await Assert.That(exception.Message).Contains("does not support event history enumeration");
	}

	[Test]
	public async Task GetEventHistoryAsync_GivenInvalidRequest_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		HistoryEnabledStore store = new([]);
		AggregateEventHistoryRequest request = new() { FromVersion = 5, ToVersion = 4 };

		// Act / Assert
		var exception = (
			await Assert.That(() => store.GetEventHistoryAsync("agg-1", request)!).Throws<ArgumentOutOfRangeException>()
		)!;

		await Assert.That(exception.ParamName).IsEqualTo("request");
	}

	static (IEvent @event, string eventType) CreateEvent(string eventType, int version, DateTimeOffset when) =>
		(
			new TestAuditEvent
			{
				Details = new EventDetails
				{
					AggregateVersion = version,
					When = when,
					IdempotencyId = $"idem-{version}",
					CorrelationId = "corr-1",
				},
			},
			eventType
		);

	sealed class TestAuditEvent : IEvent
	{
		public EventDetails Details { get; set; } = new();
	}

	sealed class HistoryEnabledStore(IEnumerable<(IEvent @event, string eventType)> events)
		: IEventStoreCore<TestAggregate>,
			IAggregateEventHistoryStoreCore<TestAggregate>
	{
		readonly IReadOnlyList<(IEvent @event, string eventType)> _events = [.. events];

		public async IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
			string aggregateId,
			int versionFrom,
			int? versionTo,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			var upperBound = versionTo ?? int.MaxValue;
			foreach (
				var item in _events.Where(m =>
					m.@event.Details.AggregateVersion >= versionFrom && m.@event.Details.AggregateVersion <= upperBound
				)
			)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield return item;
				await Task.Yield();
			}
		}

		public Task<TestAggregate> CreateAsync(
			string? aggregateId = null,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<TestAggregate?> GetOrCreateAsync(
			string? aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<TestAggregate?> GetAsync(
			string aggregateId,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<TestAggregate?> GetAtAsync(
			string aggregateId,
			int version,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<SaveResult<TestAggregate>> SaveAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default) =>
			throw new NotImplementedException();

		public Task<TestAggregate?> GetDeletedAsync(
			string aggregateId,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<bool> DeleteAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public Task<bool> RestoreAsync(
			TestAggregate aggregate,
			EventStoreOperationContext? operationContext,
			CancellationToken cancellationToken = default
		) => throw new NotImplementedException();

		public async IAsyncEnumerable<string> GetAggregateIdsAsync(
			bool includeDeleted,
			[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
		)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await Task.CompletedTask;

			yield break;
		}

		public Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default) =>
			throw new NotImplementedException();

		public TestAggregate FulfilRequirements(TestAggregate aggregate) => aggregate;
	}
}
