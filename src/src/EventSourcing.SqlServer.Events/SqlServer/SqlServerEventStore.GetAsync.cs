using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.SqlServer;

partial class SqlServerEventStore<T>
{
	[System.Diagnostics.DebuggerStepThrough]
	public Task<T?> GetAsync(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	) => GetCoreAsync(aggregateId, operationContext, cancellationToken);

	async Task<T?> GetCoreAsync(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		operationContext ??= EventStoreOperationContext.DefaultContext;

		_eventStoreTelemetry.GetAggregateStart(aggregateId, _aggregateTypeFullName);
		using var activity = _eventStoreTelemetry.GetAggregate(aggregateId, _aggregateTypeFullName);
		var getStopwatch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			var aggregate = operationContext.CacheMode.HasFlag(EventStoreCachingOptions.GetFromCache)
				? await GetFromCacheAsync(aggregateId, cancellationToken)
				: null;

			if (aggregate != null)
			{
				_eventStoreTelemetry.AggregateRetrievedFromCache(aggregateId, _aggregateTypeFullName);

				return ReturnAggregate(aggregate.Details.IsDeleted, aggregateId, operationContext)
					? PrepareAggregateForReturn(aggregate, _aggregateRequirementsManager)
					: null;
			}

			var streamVersion = await GetStreamVersionAsync(aggregateId, true, cancellationToken);
			if (streamVersion == null)
				return null;

			if (!ReturnAggregate(streamVersion.IsDeleted, aggregateId, operationContext))
				return null;

			int? streamVersionIdentifier = null;
			if (!operationContext.SkipSnapshot)
				aggregate = await GetLatestSnapshotAsync(aggregateId, cancellationToken);

			aggregate ??= new T { Details = { Id = aggregateId } };

			await GetAndApplyEventsAsync(aggregate, streamVersion, streamVersionIdentifier, cancellationToken);
			await UpdateCacheAsync(aggregate, operationContext.CacheOptions, cancellationToken);

			_eventStoreTelemetry.AggregateLoaded(aggregate.AggregateType);

			return PrepareAggregateForReturn(aggregate, _aggregateRequirementsManager);
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.GetAggregateFailed(aggregateId, _aggregateTypeFullName, ex);
			throw;
		}
		finally
		{
			getStopwatch.Stop();
			_eventStoreTelemetry.GetAggregateComplete(
				aggregateId,
				_aggregateTypeFullName,
				getStopwatch.ElapsedMilliseconds
			);
		}

		static T PrepareAggregateForReturn(
			T aggregate,
			Services.IAggregateRequirementsManager aggregateRequirementsManager
		)
		{
			var currentVersion = aggregate.Details.CurrentVersion;

			aggregate.ClearUnsavedEvents();

			aggregate.Details.CurrentVersion = currentVersion;

			aggregateRequirementsManager.Fulfil(aggregate);

			return aggregate;
		}
	}

	async Task GetAndApplyEventsAsync(
		T aggregate,
		StreamVersionData streamVersion,
		int? maxVersion,
		CancellationToken cancellationToken
	)
	{
		var aggregateId = aggregate.Id();
		var eventCount = 0;
		var eventQuery = GetEventRangeAsync(
			aggregateId,
			aggregate.Details.SnapshotVersion + 1,
			maxVersion,
			cancellationToken
		);
		await foreach (var eventResult in eventQuery)
		{
			var @event = eventResult.@event;
			if (@event is UnknownEvent || !aggregate.CanApplyEvent(@event))
			{
				var eventType = @event.GetType();
				if (@event is UnknownEvent)
					_eventStoreTelemetry.SkippedUnknownEvent(
						aggregateId,
						_aggregateTypeFullName,
						aggregate.AggregateType,
						eventResult.eventType,
						@event.Details.AggregateVersion
					);
				else
					_eventStoreTelemetry.CannotApplyEvent(
						aggregateId,
						_aggregateTypeFullName,
						aggregate.AggregateType,
						eventResult.eventType,
						eventType.FullName ?? eventType.Name,
						@event.Details.AggregateVersion
					);

				aggregate.Details.CurrentVersion = @event.Details.AggregateVersion;
			}
			else
				aggregate.ApplyEvent(@event);

			eventCount++;
		}

		_eventStoreTelemetry.ReconstitutedAggregateFromEvents(
			aggregateId,
			_aggregateTypeFullName,
			aggregate.AggregateType,
			eventCount,
			AggregateVersionData.Create(aggregate)
		);

		aggregate.Details.SavedVersion = aggregate.Details.CurrentVersion;
		aggregate.Details.Etag = streamVersion.Version.ToString(CultureInfo.InvariantCulture);

		aggregate.Details.IsDeleted = streamVersion.IsDeleted;
	}

	async Task<T?> GetLatestSnapshotAsync(string aggregateId, CancellationToken cancellationToken)
	{
		try
		{
			var snapshotId = CreateSnapshotId(aggregateId);
			var row = await _client.GetByIdAsync(snapshotId, cancellationToken);
			if (row == null || row.EntityType != SnapshotType || string.IsNullOrWhiteSpace(row.Payload))
				return null;

			return DeserializeSnapshot(row.Payload);
		}
		#pragma warning disable CA1031
		catch (Exception ex)
		#pragma warning restore CA1031
		{
			_eventStoreTelemetry.SnapshotDeserializationFailed(aggregateId, _aggregateTypeFullName, ex);

			return null;
		}
	}

	async Task<T?> GetFromCacheAsync(string aggregateId, CancellationToken cancellationToken)
	{
		T? aggregate = null;
		try
		{
			var cacheKey = CreateCacheKey(aggregateId);
			var snapshotData = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
			if (!string.IsNullOrWhiteSpace(snapshotData))
			{
				aggregate = DeserializeSnapshot(snapshotData);
				aggregate.Details.SavedVersion = aggregate.Details.CurrentVersion;
			}
		}
		#pragma warning disable CA1031
		catch (Exception ex)
		#pragma warning restore CA1031
		{
			_eventStoreTelemetry.CacheGetFailure(aggregateId, _aggregateTypeFullName, ex);
		}

		return aggregate;
	}
}
