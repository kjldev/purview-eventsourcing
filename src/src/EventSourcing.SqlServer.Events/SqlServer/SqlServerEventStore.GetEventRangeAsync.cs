using System.Runtime.CompilerServices;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.SqlServer;

partial class SqlServerEventStore<T>
{
    /// <summary>
    /// Gets a range of <see cref="IEvent"/>s for a given aggregate, as specified by it's <paramref name="aggregateId"/>.
    /// </summary>
    /// <param name="aggregateId">The id of the aggregate.</param>
    /// <param name="versionFrom">The inclusive event number to start the range at.</param>
    /// <param name="versionTo">Optional, the inclusive event number to finish the range at.</param>
    /// <param name="cancellationToken">The stopping token.</param>
    /// <returns>If no <paramref name="versionTo"/> is specified all available events greater than <paramref name="versionFrom"/> are returned.</returns>
    public async IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
        string aggregateId,
        int versionFrom,
        int? versionTo,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));
        if (versionFrom < 1)
            throw new ArgumentOutOfRangeException(
                nameof(versionFrom),
                versionFrom,
                $"{nameof(versionFrom)} must be greater than 0."
            );

        if (versionTo < versionFrom)
            throw new ArgumentOutOfRangeException(
                nameof(versionTo),
                versionTo.Value,
                $"{nameof(versionTo)} ({versionTo}) must be greater than or equal to {nameof(versionFrom)} ({versionFrom})."
            );

        var aggregateVersion = versionFrom;
        var effectiveVersionTo = versionTo ?? int.MaxValue;
        var entities = _client.GetEventRangeAsync(
            aggregateId,
            versionFrom,
            effectiveVersionTo,
            cancellationToken
        );
        await foreach (var entity in entities)
        {
            var item = DeserializeEvent(entity, aggregateVersion);
            if (item != null)
                yield return (item, entity.EventType!);

            aggregateVersion++;
        }
    }

    /// <param name="aggregateVersion">Only used when an unknown event is found.</param>
    IEvent? DeserializeEvent(SqlServerEventStoreClient.RowData eventRow, int aggregateVersion)
    {
        static UnknownEvent ReturnUnknownEvent(
            SqlServerEventStoreClient.RowData eventRow,
            int aggregateVersion
        )
        {
            return new UnknownEvent
            {
                Details =
                {
                    When = eventRow.Timestamp,
                    AggregateVersion = aggregateVersion,
                    IdempotencyId = eventRow.IdempotencyId,
                },
                Payload = eventRow.Payload,
            };
        }

        try
        {
            var eventType = _eventNameMapper.GetTypeName<T>(eventRow.EventType!);
            if (eventType == null)
            {
                _eventStoreTelemetry.MissingEventType(_aggregateTypeFullName, eventRow.EventType!);

                return ReturnUnknownEvent(eventRow, aggregateVersion);
            }

            var runtimeEventType =
                Type.GetType(eventType, throwOnError: false)
                ?? throw new ApplicationException($"Unable to load event type: {eventType}");
            var @event = DeserializeEvent(eventRow.Payload!, runtimeEventType);

            // Apply upcasting chain when a registry is available.
            if (@event != null && _eventUpcasterRegistry?.CanUpcast(@event) == true)
                @event = _eventUpcasterRegistry.Upcast(@event);

            return @event;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _eventStoreTelemetry.EventDeserializationFailed(
                eventRow.AggregateId,
                _aggregateTypeFullName,
                ex
            );

            return ReturnUnknownEvent(eventRow, aggregateVersion);
        }
    }
}
