using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using FluentValidation.Results;
using MongoDB.Driver;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.MongoDB.Entities;
using Purview.EventSourcing.MongoDB.StorageClients;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	[DebuggerStepThrough]
	public Task<SaveResult<T>> SaveAsync([NotNull] T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
		=> SaveCoreAsync(aggregate, operationContext, cancellationToken);

	async Task<SaveResult<T>> SaveCoreAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken, params IEvent[] additionalEvents)
	{
		operationContext ??= EventStoreOperationContext.DefaultContext;

		FulfilRequirements(aggregate);

		var idempotencyId = operationContext.CorrelationId ?? Activity.Current?.Id ?? $"{Guid.NewGuid()}";
		var validationResult = await GuardAsync(aggregate, cancellationToken);

		static SaveResult<T> ReturnSaveResult(T a, bool success, bool skipped, ValidationResult? validationResult = null) => new(a, validationResult ?? new ValidationResult(), success, skipped);

		if (!validationResult.IsValid)
			return ReturnSaveResult(aggregate, false, false, validationResult);

		if (aggregate.Details.Locked)
		{
			return operationContext.LockMode is LockHandlingMode.ThrowsException
				? throw new Exceptions.AggregateLockedException(idempotencyId)
				: ReturnSaveResult(aggregate, false, false);
		}

		if (string.IsNullOrWhiteSpace(aggregate.Details.Id))
			throw new Exceptions.MissingAggregateIdException(idempotencyId);

		_eventStoreTelemetry.SaveCalled(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);
		if (!aggregate.HasUnsavedEvents() && (additionalEvents?.Length ?? 0) == 0)
		{
			_eventStoreTelemetry.SaveContainedNoChanges(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);

			return ReturnSaveResult(aggregate, false, true);
		}

		var isNew = aggregate.IsNew();
		var changeEvents = aggregate.GetUnsavedEvents().Concat((additionalEvents ?? []).AsEnumerable()).ToArray();
		var idempotencyMarkerOperation = CreateIdempotencyMarkerOperation(aggregate, idempotencyId, changeEvents);

		if (changeEvents.Length > _eventStoreOptions.Value.MaxEventCountOnSave)
			throw new ArgumentOutOfRangeException($"The maximum amount of events to save was exceeded. Attempted: {changeEvents.Length}, Maximum: {_eventStoreOptions.Value.MaxEventCountOnSave}");

		if (operationContext.UseIdempotencyMarker)
		{
			var exists = (await _eventClient.GetAsync<IdempotencyMarkerEntity>(idempotencyMarkerOperation.AggregateId, cancellationToken)) != null;
			if (exists)
			{
				_eventStoreTelemetry.EventsAlreadyApplied(aggregate.Id(), idempotencyId);
				return ReturnSaveResult(aggregate, true, true);
			}
		}

		if (operationContext.NotificationMode.HasFlag(NotificationModes.BeforeDelete) && changeEvents.OfType<DeleteEvent>().Any())
			await _aggregateChangeNotifier.BeforeDeleteAsync(aggregate, cancellationToken);
		else if (operationContext.NotificationMode.HasFlag(NotificationModes.BeforeSave))
			await _aggregateChangeNotifier.BeforeSaveAsync(aggregate, isNew, cancellationToken);

		var streamEntity = await GetStreamVersionAsync(aggregate.Id(), !isNew, cancellationToken);
		var hasStreamEntity = streamEntity != null;
		if (streamEntity?.IsDeleted == true)
		{
			var throwIfDeleted = !changeEvents.OfType<RestoreEvent>().Any();
			if (throwIfDeleted)
				throw new Exceptions.AggregateDeletedException(aggregate.Id(), idempotencyId);
		}

		try
		{
			var previousAggregateVersion = aggregate.Details.SavedVersion;
			var shouldSnapshot = ShouldSnapShot(aggregate, changeEvents);

			BatchOperation batchOperation = new();
			streamEntity = new()
			{
				Id = streamEntity?.Id ?? CreateStreamVersionId(aggregate.Id()),
				AggregateId = aggregate.Id(),
				IsDeleted = aggregate.Details.IsDeleted,
				AggregateType = aggregate.AggregateType,
				Version = aggregate.Details.CurrentVersion,
				Timestamp = DateTimeOffset.UtcNow
			};

			if (isNew || !hasStreamEntity)
				batchOperation.Insert(streamEntity);
			else
				batchOperation.Update(streamEntity);

			var userId = ClaimsPrincipal.Current?.FindFirst(operationContext.ClaimIdentifier)?.Value;
			if (operationContext.RequiresValidPrincipalIdentifier && string.IsNullOrWhiteSpace(userId))
				throw new NullReferenceException($"Missing ClaimsPrincipal identifier '{operationContext.ClaimIdentifier}'. Unable to save aggregate.");

			var idempotencyIdAsString = idempotencyId.ToUpperInvariant();
			Dictionary<string, IEvent> largeChangeEvents = [];
			for (var i = 0; i < changeEvents.Length; i++)
			{
				var changeEvent = changeEvents[i];

				changeEvent.Details.IdempotencyId = idempotencyIdAsString;
				changeEvent.Details.UserId = userId;

				var serializedEvent = SerializeEvent(changeEvent);
				var eventEntity = CreateSerializedEvent(aggregate.Id(), changeEvent, serializedEvent, idempotencyMarkerOperation.AggregateId);

				batchOperation.Insert(eventEntity);
			}

			batchOperation.Insert(idempotencyMarkerOperation);

			await SubmitBatchOperationsAsync(aggregate, idempotencyId, batchOperation, cancellationToken);

			// We create a snapshot if it's been deleted or restored, could make searching easier later on.
			if (shouldSnapshot)
				await CreateSnapshotAsync(aggregate, cancellationToken);

			if (changeEvents.OfType<DeleteEvent>().Any())
				_eventStoreTelemetry.AggregateDeleted(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);
			else if (changeEvents.OfType<RestoreEvent>().Any())
				_eventStoreTelemetry.AggregateRestored(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);

			_eventStoreTelemetry.SavedAggregate(aggregate.Id(), _aggregateTypeFullName, changeEvents.Length, aggregate.AggregateType);

			// Do not pass in the cancellation token. We want this to carry on as long as possible.
			await UpdateCacheAsync(aggregate, operationContext.CacheOptions);

			// ...or here.
			if (aggregate.Details.IsDeleted && operationContext.NotificationMode.HasFlag(NotificationModes.AfterDelete))
				await _aggregateChangeNotifier.AfterDeleteAsync(aggregate);
			else if (operationContext.NotificationMode.HasFlag(NotificationModes.AfterSave))
				await _aggregateChangeNotifier.AfterSaveAsync(aggregate, previousAggregateVersion, isNew, changeEvents);
		}
		catch (Exception ex)
		{
			ClearCacheFireAndForget(aggregate);

			if (operationContext.NotificationMode.HasFlag(NotificationModes.OnFailure))
			{
				var deleteRequested = changeEvents.OfType<DeleteEvent>().Any();
				await _aggregateChangeNotifier.FailureAsync(aggregate, deleteRequested, ex);
			}

			throw;
		}

		return ReturnSaveResult(aggregate, true, false);
	}

	async Task<ValidationResult> GuardAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		var validationResult = _validator == null
			? await DefaultAggregateValidator<T>.Instance.ValidateAsync(aggregate, cancellationToken)
			: await _validator.ValidateAsync(aggregate, cancellationToken);

		return validationResult;
	}

	bool ShouldSnapShot(T aggregate, IEvent[] events)
	{
		if (aggregate.Details.IsDeleted || events.OfType<RestoreEvent>().Any())
			return true;

		return (aggregate.Details.CurrentVersion - aggregate.Details.SnapshotVersion) >= _eventStoreOptions.Value.SnapshotInterval;
	}

	IdempotencyMarkerEntity CreateIdempotencyMarkerOperation(T aggregate, string idempotencyId, IEvent[] changeEvents)
	{
		IdempotencyMarkerEntity marker = new()
		{
			Id = CreateIdempotencyCheckId(aggregate.Id(), idempotencyId),
			AggregateId = aggregate.Id(),
			EventVersions = [.. changeEvents.Select(m => m.Details.AggregateVersion).OrderBy(m => m)],
			Timestamp = DateTimeOffset.UtcNow
		};

		return marker;
	}

	async Task SubmitBatchOperationsAsync(T aggregate, string idempotencyId, BatchOperation batchOperation, CancellationToken cancellationToken)
	{
		try
		{
			await _eventClient.SubmitBatchAsync(batchOperation, cancellationToken);

			var currentVersion = aggregate.Details.CurrentVersion;

			aggregate.ClearUnsavedEvents();

			aggregate.Details.CurrentVersion = aggregate.Details.SavedVersion = currentVersion;
			aggregate.Details.Etag = currentVersion.ToString(CultureInfo.InvariantCulture);
		}
		catch (MongoWriteException ex)
		{
			_eventStoreTelemetry.SaveFailedAtStorage(aggregate.Id(), _aggregateTypeFullName, ex);

			ClearCacheFireAndForget(aggregate);

			throw new Exceptions.CommitException(aggregate.Id(), idempotencyId, aggregate.Details.CurrentVersion, aggregate.Details.SavedVersion, ex);
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.SaveFailed(aggregate.Id(), _aggregateTypeFullName, ex);

			ClearCacheFireAndForget(aggregate);

			throw;
		}
	}

	async Task CreateSnapshotAsync(T aggregate, CancellationToken cancellationToken)
	{
		// Set the snapshot version to the current version...
		aggregate.Details.SnapshotVersion = aggregate.Details.CurrentVersion;

		var snapshot = SerializeSnapshot(aggregate);

		SnapshotEntity snapshotEntity = new()
		{
			Id = aggregate.Id(),
			AggregateType = _aggregateTypeShortName,
			AggregateFullType = _aggregateTypeFullName,
			Timestamp = DateTimeOffset.UtcNow,
			Payload = snapshot
		};

		await _snapshotClient.UpsertAsync(snapshotEntity, m => m.Id == snapshotEntity.Id && m.EntityType == EntityTypes.SnapshotType, cancellationToken);
	}

	EventEntity CreateSerializedEvent(string aggregateId, IEvent @event, string serializedEvent, string idempotencyId)
		=> new()
		{
			Id = CreateEventId(aggregateId, @event.Details.AggregateVersion),
			AggregateId = aggregateId,
			Version = @event.Details.AggregateVersion,
			Payload = serializedEvent,
			EventType = _eventNameMapper.GetName<T>(@event),
			IdempotencyId = idempotencyId,
			Timestamp = DateTimeOffset.UtcNow
		};

	void ClearCacheFireAndForget(T aggregate)
	{
		Task.Run(async () =>
		{
			try
			{
				var cacheKey = CreateCacheKey(aggregate.Id());
				// Do not pass in the cancellation token. We want this to carry on as long as possible.
				await _distributedCache.RemoveAsync(cacheKey);
			}
			catch (Exception ex)
			{
				_eventStoreTelemetry.CacheRemovalFailure(aggregate.Id(), _aggregateTypeFullName, ex);
			}
		});
	}
}
