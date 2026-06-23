using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing;

[System.Diagnostics.DebuggerStepThrough]
public static class IEventStoreCoreHistoryExtensions
{
	const int MaxAllowedPageSize = 1000;

	public static async Task<ContinuationResponse<AggregateEventHistoryItem>> GetEventHistoryAsync<T>(
		[NotNull] this IEventStoreCore<T> eventStore,
		string aggregateId,
		AggregateEventHistoryRequest? request = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

		request ??= new AggregateEventHistoryRequest();
		ValidateRequest(request);

		if (eventStore is not IAggregateEventHistoryStoreCore<T> historyStore)
		{
			throw new NotSupportedException(
				$"The configured event store for aggregate type '{typeof(T).FullName}' does not support event history enumeration."
			);
		}

		var effectiveMaxRecords = request.MaxRecords;
		var continuationOffset = ParseContinuationOffset(request.ContinuationToken);
		var versionFrom = request.FromVersion ?? 1;
		var versionTo = request.ToVersion;

		List<AggregateEventHistoryItem> items = [];
		var matchedCount = 0;
		var hasMore = false;
		await foreach (
			var (@event, eventType) in historyStore.GetEventRangeAsync(
				aggregateId,
				versionFrom,
				versionTo,
				cancellationToken
			)
		)
		{
			var details = @event.Details;
			if (request.FromUtc.HasValue && details.When < request.FromUtc.Value)
				continue;

			if (request.ToUtc.HasValue && details.When > request.ToUtc.Value)
				continue;

			if (matchedCount < continuationOffset)
			{
				matchedCount++;
				continue;
			}

			if (items.Count >= effectiveMaxRecords)
			{
				hasMore = true;
				break;
			}

			items.Add(ToHistoryItem<T>(aggregateId, eventType, @event));
			matchedCount++;
		}

		var token = hasMore ? (continuationOffset + items.Count).ToString(CultureInfo.InvariantCulture) : null;

		return new ContinuationResponse<AggregateEventHistoryItem>
		{
			Results = [.. items],
			RequestedCount = effectiveMaxRecords,
			ContinuationToken = token,
		};
	}

	static AggregateEventHistoryItem ToHistoryItem<T>(string aggregateId, string eventType, IEvent @event)
		where T : class, IAggregate, new()
	{
		var details = @event.Details;
		var payload = @event is EventUnknown unknown
			? unknown.Payload
			: JsonSerializer.Serialize(@event, @event.GetType());

		return new AggregateEventHistoryItem
		{
			AggregateId = aggregateId,
			AggregateType = typeof(T).Name,
			EventType = eventType,
			EventClrType = @event.GetType().FullName ?? @event.GetType().Name,
			AggregateVersion = details.AggregateVersion,
			When = details.When,
			IdempotencyId = details.IdempotencyId,
			UserId = details.UserId,
			CausationId = details.CausationId,
			CorrelationId = details.CorrelationId,
			IsUnknownEvent = @event is EventUnknown,
			Payload = payload,
		};
	}

	static int ParseContinuationOffset(string? continuationToken)
	{
		if (string.IsNullOrWhiteSpace(continuationToken))
			return 0;

		if (
			!int.TryParse(continuationToken, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
			|| offset < 0
		)
		{
			throw new ArgumentOutOfRangeException(
				nameof(continuationToken),
				continuationToken,
				"Continuation token must be a non-negative integer offset."
			);
		}

		return offset;
	}

	static void ValidateRequest(AggregateEventHistoryRequest request)
	{
		if (request.MaxRecords < 1 || request.MaxRecords > MaxAllowedPageSize)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.MaxRecords,
				$"{nameof(request.MaxRecords)} must be between 1 and {MaxAllowedPageSize}."
			);
		}

		if (request.FromVersion is < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.FromVersion,
				$"{nameof(request.FromVersion)} must be greater than 0 when provided."
			);
		}

		if (request.ToVersion is < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.ToVersion,
				$"{nameof(request.ToVersion)} must be greater than 0 when provided."
			);
		}

		if (
			request.FromVersion.HasValue
			&& request.ToVersion.HasValue
			&& request.ToVersion.Value < request.FromVersion.Value
		)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.ToVersion,
				$"{nameof(request.ToVersion)} ({request.ToVersion}) must be greater than or equal to {nameof(request.FromVersion)} ({request.FromVersion})."
			);
		}

		if (request.FromUtc.HasValue && request.ToUtc.HasValue && request.ToUtc.Value < request.FromUtc.Value)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.ToUtc,
				$"{nameof(request.ToUtc)} ({request.ToUtc}) must be greater than or equal to {nameof(request.FromUtc)} ({request.FromUtc})."
			);
		}
	}
}
