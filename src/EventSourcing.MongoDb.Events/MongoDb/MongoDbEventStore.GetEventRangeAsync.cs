using System.Runtime.CompilerServices;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.MongoDB.Entities;

namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	/// <summary>
	/// Gets a range of <see cref="IEvent"/>s for a given aggregate, as specified by it's <paramref name="aggregateId"/>.
	/// </summary>
	/// <param name="aggregateId">The id of the <see cref="Interfaces.Aggregates.IAggregate"/>.</param>
	/// <param name="versionFrom">The inclusive event number to start the range at.</param>
	/// <param name="versionTo">Optional, the inclusive event number to finish the range at.</param>
	/// <param name="cancellationToken">The stopping token.</param>
	/// <returns>If no <paramref name="versionFrom"/> is specified all available events greater than <paramref name="versionFrom"/> are returned.</returns>
	public async IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(string aggregateId, int versionFrom, int? versionTo, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));
		if (versionFrom < 1)
			throw new ArgumentOutOfRangeException(nameof(versionFrom), versionFrom, $"{nameof(versionFrom)} must be greater than 0.");

		if (versionTo < versionFrom)
			throw new ArgumentOutOfRangeException(nameof(versionTo), versionTo.Value, $"{nameof(versionTo)} ({versionTo}) must be greater than or equal to ${nameof(versionFrom)} ({versionFrom}).");

		var aggregateVersion = versionFrom;
		var entities = GetEventRangeEntitiesAsync(aggregateId, versionFrom, versionTo, cancellationToken);
		await foreach (var entity in entities)
		{
			var item = DeserializeEvent(entity, aggregateVersion);
			if (item != null)
				yield return (item, entity.EventType);

			aggregateVersion++;
		}
	}

	internal async IAsyncEnumerable<EventEntity> GetEventRangeEntitiesAsync(string aggregateId, int versionFrom, int? versionTo, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		versionTo ??= int.MaxValue;

		var query = _eventClient.QueryEnumerableAsync<EventEntity>(m => m.AggregateId == aggregateId
			&& m.EntityType == EntityTypes.EventType
			&& m.Version >= versionFrom && m.Version <= versionTo,
			orderByClause: m => m.OrderBy(e => e.Version),
			cancellationToken: cancellationToken);

		await foreach (var eventEntity in query)
			yield return eventEntity!;
	}

	/// <param name="aggregateVersion">Only used when an unknown event is found.</param>
	IEvent? DeserializeEvent(EventEntity eventEntity, int aggregateVersion)
	{
		static UnknownEvent ReturnUnknownEvent(EventEntity eventEntity, int aggregateVersion)
		{
			return new UnknownEvent
			{
				Details = {
					When = eventEntity.Timestamp!.Value,
					AggregateVersion = aggregateVersion,
					IdempotencyId = eventEntity.IdempotencyId
				},
				Payload = eventEntity.Payload
			};
		}

		try
		{
			var eventType = _eventNameMapper.GetTypeName<T>(eventEntity.EventType);
			if (eventType == null)
			{
				_eventStoreTelemetry.MissingEventType(_aggregateTypeFullName, eventEntity.EventType);

				return ReturnUnknownEvent(eventEntity, aggregateVersion);
			}

			var runtimeEventType = Type.GetType(eventType, throwOnError: false) ?? throw new ApplicationException($"Unable to load event type: {eventType}");
			var @event = DeserializeEvent(eventEntity.Payload, runtimeEventType);

			return @event;
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.EventDeserializationFailed(eventEntity.AggregateId, _aggregateTypeFullName, ex);

			return ReturnUnknownEvent(eventEntity, aggregateVersion);
		}
	}
}
