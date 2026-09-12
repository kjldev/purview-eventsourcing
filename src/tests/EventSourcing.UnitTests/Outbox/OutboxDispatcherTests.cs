using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Purview.EventSourcing.Outbox;

public sealed class OutboxDispatcherTests
{
	[Test]
	public async Task ComputeBackoff_DoublesPerAttempt()
	{
		var backoff = OutboxDispatcher.ComputeBackoff(TimeSpan.FromSeconds(5), attempt: 1);
		await Assert.That(backoff).IsEqualTo(TimeSpan.FromSeconds(5));

		var second = OutboxDispatcher.ComputeBackoff(TimeSpan.FromSeconds(5), attempt: 2);
		await Assert.That(second).IsEqualTo(TimeSpan.FromSeconds(10));

		var capped = OutboxDispatcher.ComputeBackoff(TimeSpan.FromSeconds(5), attempt: 10);
		await Assert.That(capped).IsEqualTo(TimeSpan.FromSeconds(5) * 64);
	}

	[Test]
	public async Task Validate_GivenInvalidOptions_Throws()
	{
		OutboxDispatchOptions invalid = new() { BatchSize = 0 };
		await Assert.That(invalid.Validate).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Validate_GivenValidOptions_DoesNotThrow()
	{
		OutboxDispatchOptions valid = new();
		await Assert.That(valid.Validate).ThrowsNothing();
	}

	[Test]
	public async Task DispatchAsync_GivenMessages_CompletesEachAndReturnsCounts()
	{
		InMemoryOutboxStore store = new();
		RecordingHandler handler = new();
		var dispatcher = CreateDispatcher(store, handler);

		store.Add(Message("a", "{}"));
		store.Add(Message("b", "{}"));

		var result = await dispatcher.DispatchAsync(CancellationToken.None);

		await Assert.That(result.Claimed).IsEqualTo(2);
		await Assert.That(result.Dispatched).IsEqualTo(2);
		await Assert.That(result.Failed).IsEqualTo(0);
		await Assert.That(result.Poisoned).IsEqualTo(0);
		await Assert.That(handler.HandledIds).IsEquivalentTo(["a", "b"]);
		await Assert.That(store.GetState("a")).IsEqualTo(OutboxState.Dispatched);
	}

	[Test]
	public async Task DispatchAsync_GivenFailingHandler_MarksFailedWithBackoff()
	{
		InMemoryOutboxStore store = new();
		FailingHandler handler = new();
		var dispatcher = CreateDispatcher(store, handler);

		store.Add(Message("a", "{}"));

		var result = await dispatcher.DispatchAsync(CancellationToken.None);

		await Assert.That(result.Failed).IsEqualTo(1);
		await Assert.That(store.GetState("a")).IsEqualTo(OutboxState.Failed);
		await Assert.That(store.GetAttempts("a")).IsEqualTo(1);
		await Assert.That(store.GetNextAttempt("a")).IsNotNull();
	}

	[Test]
	public async Task DispatchAsync_GivenMaxAttemptsReached_MovesToPoisoned()
	{
		InMemoryOutboxStore store = new();
		FailingHandler handler = new();
		var dispatcher = CreateDispatcher(
			store,
			handler,
			configure: static options =>
			{
				options.MaxAttempts = 1;
				options.RetryBackoffBase = TimeSpan.Zero;
			}
		);

		store.Add(Message("a", "{}"));

		var result = await dispatcher.DispatchAsync(CancellationToken.None);

		await Assert.That(result.Poisoned).IsEqualTo(1);
		await Assert.That(store.GetState("a")).IsEqualTo(OutboxState.Poisoned);
		await Assert.That(store.GetError("a")).IsNotEmpty();
	}

	[Test]
	public async Task DispatchAsync_GivenCancellation_Cancels()
	{
		InMemoryOutboxStore store = new();
		CancellingHandler handler = new();
		var dispatcher = CreateDispatcher(store, handler);

		store.Add(Message("a", "{}"));

		await Assert
			.That(() => (Task)dispatcher.DispatchAsync(new CancellationToken(canceled: true)))
			.Throws<OperationCanceledException>();
	}

	static OutboxDispatcher CreateDispatcher(
		InMemoryOutboxStore store,
		IOutboxHandler handler,
		Action<OutboxDispatchOptions>? configure = null
	)
	{
		OutboxDispatchOptions options = new();
		configure?.Invoke(options);
		return new OutboxDispatcher(store, handler, Options.Create(options), NullLogger<OutboxDispatcher>.Instance);
	}

	static OutboxEnvelope Message(string id, string payload) =>
		new(
			id,
			"OrderAggregate",
			"order-1",
			"OrderCreated",
			payload,
			IdempotencyKey: null,
			CorrelationId: null,
			CreatedUtc: DateTimeOffset.UtcNow
		);

	sealed class RecordingHandler : IOutboxHandler
	{
		public List<string> HandledIds { get; } = [];

		public Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
		{
			HandledIds.Add(message.Id);
			return Task.CompletedTask;
		}
	}

	sealed class FailingHandler : IOutboxHandler
	{
		public Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("handler failed");
	}

	sealed class CancellingHandler : IOutboxHandler
	{
		public Task HandleAsync(OutboxEnvelope message, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}
	}
}

/// <summary>
/// Thread-safe in-memory outbox store used to exercise the dispatcher without a database.
/// </summary>
sealed class InMemoryOutboxStore : IOutboxStore
{
	readonly ConcurrentDictionary<string, OutboxEnvelope> _messages = new(StringComparer.Ordinal);

	public void Add(OutboxEnvelope message) => _messages[message.Id] = message;

	public Task<int> EnqueueAsync(OutboxEnvelope message, CancellationToken cancellationToken) =>
		Task.FromResult(_messages.TryAdd(message.Id, message) ? 1 : 0);

	public Task<IReadOnlyList<OutboxEnvelope>> ClaimNextBatchAsync(
		string leaseOwner,
		DateTimeOffset leaseUntil,
		int batchSize,
		CancellationToken cancellationToken
	)
	{
		var now = DateTimeOffset.UtcNow;
		var batch = _messages
			.Values.Where(message =>
				(message.State is OutboxState.Pending or OutboxState.Failed)
				&& (message.LeaseExpiresUtc is null || message.LeaseExpiresUtc < now)
				&& (message.NextAttemptUtc is null || message.NextAttemptUtc <= now)
			)
			.OrderBy(static message => message.CreatedUtc)
			.ThenBy(static message => message.Id, StringComparer.Ordinal)
			.Take(batchSize)
			.ToArray();

		foreach (var message in batch)
			_messages[message.Id] = message with { LeaseExpiresUtc = leaseUntil };

		return Task.FromResult<IReadOnlyList<OutboxEnvelope>>(batch);
	}

	public Task CompleteAsync(string id, CancellationToken cancellationToken)
	{
		if (_messages.TryGetValue(id, out var message))
			_messages[id] = message with
			{
				State = OutboxState.Dispatched,
				DispatchedUtc = DateTimeOffset.UtcNow,
				LeaseExpiresUtc = null,
			};

		return Task.CompletedTask;
	}

	public Task MarkFailedAsync(
		string id,
		string errorMessage,
		DateTimeOffset nextAttemptUtc,
		CancellationToken cancellationToken
	)
	{
		if (_messages.TryGetValue(id, out var message))
			_messages[id] = message with
			{
				State = OutboxState.Failed,
				AttemptCount = message.AttemptCount + 1,
				NextAttemptUtc = nextAttemptUtc,
				LeaseExpiresUtc = null,
				LastError = errorMessage,
			};

		return Task.CompletedTask;
	}

	public Task MarkPoisonedAsync(string id, string errorMessage, CancellationToken cancellationToken)
	{
		if (_messages.TryGetValue(id, out var message))
			_messages[id] = message with
			{
				State = OutboxState.Poisoned,
				LeaseExpiresUtc = null,
				LastError = errorMessage,
			};

		return Task.CompletedTask;
	}

	public Task<int> CleanupAsync(TimeSpan retention, CancellationToken cancellationToken)
	{
		var cutoff = DateTimeOffset.UtcNow - retention;
		var removed = _messages
			.Values.Where(message =>
				(message.State is OutboxState.Dispatched or OutboxState.Poisoned) && message.CreatedUtc < cutoff
			)
			.Select(static message => message.Id)
			.ToArray();
		foreach (var id in removed)
			_messages.TryRemove(id, out _);

		return Task.FromResult(removed.Length);
	}

	public Task<IReadOnlyList<OutboxEnvelope>> GetPoisonedAsync(int skip, int take, CancellationToken cancellationToken)
	{
		var poisoned = _messages
			.Values.Where(static message => message.State == OutboxState.Poisoned)
			.OrderByDescending(static message => message.CreatedUtc)
			.ThenByDescending(static message => message.Id, StringComparer.Ordinal)
			.Skip(skip)
			.Take(take)
			.ToArray();
		return Task.FromResult<IReadOnlyList<OutboxEnvelope>>(poisoned);
	}

	public OutboxState GetState(string id) => _messages[id].State;

	public int GetAttempts(string id) => _messages[id].AttemptCount;

	public DateTimeOffset? GetNextAttempt(string id) => _messages[id].NextAttemptUtc;

	public string? GetError(string id) => _messages[id].LastError;
}
