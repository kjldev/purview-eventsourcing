using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.AzureStorage.Entities;

namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
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
			var item = await DeserializeEventAsync(entity, aggregateVersion, cancellationToken);
			if (item != null)
				yield return (item, entity.EventType);

			aggregateVersion++;
		}
	}

	internal async IAsyncEnumerable<EventEntity> GetEventRangeEntitiesAsync(string aggregateId, int versionFrom, int? versionTo, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// This query can't be done with LINQ, so don't try.
		var filter = $"({nameof(ITableEntity.PartitionKey)} eq '{aggregateId}') and (({nameof(ITableEntity.RowKey)} ge '{CreateEventRowKey(versionFrom)}') and ({nameof(ITableEntity.RowKey)} le '{CreateEventRowKey(versionTo ?? int.MaxValue)}'))";
		var query = _tableClient.QueryEnumerableAsync<EventEntity>(filter, cancellationToken: cancellationToken);
		await foreach (var eventEntity in query)
			yield return eventEntity!;
	}

	async Task<IEvent?> DeserializeEventAsync(EventEntity eventEntity, int aggregateVersion, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		static UnknownEvent ReturnUnknownEvent(EventEntity eventEntity, int aggregateVersion)
		{
			return new()
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
			if (@event is Events.LargeEventPointerEvent blobPointer)
			{
				var blobName = GenerateEventBlobName(eventEntity.PartitionKey, eventEntity.RowKey);
				var exists = await _blobClient.ExistsAsync(blobName, cancellationToken);
				if (!exists)
				{
					_eventStoreTelemetry.SkippedMissingBlobEvent(eventEntity.PartitionKey, eventEntity.RowKey, blobPointer.SerializedEventType, blobName);
					return null;
				}

				var blobEventTypeName = _eventNameMapper.GetTypeName<T>(blobPointer.SerializedEventType);
				if (string.IsNullOrWhiteSpace(blobEventTypeName))
				{
					_eventStoreTelemetry.SkippedMissingBlobEventName(eventEntity.PartitionKey, eventEntity.RowKey, blobPointer.SerializedEventType, blobName);
					return ReturnUnknownEvent(eventEntity, aggregateVersion);
					//throw new ArgumentNullException($"Unable to locate blob event type name {blobPointer.SerializedEventType}");
				}

				var blobEvent = Type.GetType(blobEventTypeName, throwOnError: false);
				if (blobEvent == null)
				{
					_eventStoreTelemetry.MissingBlobEventType(_aggregateTypeFullName, eventEntity.EventType, blobPointer.SerializedEventType, blobEventTypeName);
					return ReturnUnknownEvent(eventEntity, aggregateVersion);
				}
				//throw new ArgumentNullException($"Unable to locate blob event type {blobEventTypeName}");

				var eventStream = await _blobClient.GetStreamAsync(blobName, cancellationToken);
				if (eventStream == null)
					return ReturnUnknownEvent(eventEntity, aggregateVersion);

				string? eventContent;
				using (StreamReader reader = new(eventStream))
					eventContent = await reader.ReadToEndAsync(cancellationToken);

				return DeserializeEvent(eventContent, blobEvent);
			}

			return @event;
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.EventDeserializationFailed(eventEntity.PartitionKey, _aggregateTypeFullName, ex);

			return ReturnUnknownEvent(eventEntity, aggregateVersion);
		}
	}
}
