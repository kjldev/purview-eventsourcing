using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Text;
using Azure;
using FluentValidation.Results;
using Newtonsoft.Json;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.AzureStorage.Entities;
using Purview.EventSourcing.AzureStorage.Events;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStore<T>
{
	[DebuggerStepThrough]
	public Task<SaveResult<T>> SaveAsync([NotNull] T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken = default)
		=> SaveCoreAsync(aggregate, operationContext, cancellationToken);

	async Task<SaveResult<T>> SaveCoreAsync(T aggregate, EventStoreOperationContext? operationContext, CancellationToken cancellationToken, params IEvent[] additionalEvents)
	{
		operationContext ??= EventStoreOperationContext.DefaultContext;

		FulfilRequirements(aggregate);

		var idempotencyId = Activity.Current?.Id ?? $"{Guid.NewGuid()}";
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
			var exists = await _tableClient.EntityExistsAsync(idempotencyMarkerOperation.PartitionKey, idempotencyMarkerOperation.RowKey, cancellationToken);
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
				PartitionKey = aggregate.Id(),
				RowKey = TableEventStoreConstants.StreamVersionRowKey,
				ETag = streamEntity?.ETag ?? ETag.All,
				IsDeleted = aggregate.Details.IsDeleted,
				AggregateType = aggregate.AggregateType,
				Version = aggregate.Details.CurrentVersion
			};

			if (isNew || !hasStreamEntity)
				batchOperation.Add(streamEntity);
			else
				batchOperation.Update(streamEntity, merge: false);

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
				var eventEntity = CreateSerializedEvent(aggregate.Id(), changeEvent, serializedEvent, idempotencyMarkerOperation.RowKey);
				if (Encoding.UTF8.GetByteCount(serializedEvent) >= _maxEventSize)
				{
					LargeEventPointerEvent largeEventPointer = new()
					{
						SerializedEventType = _eventNameMapper.GetName<T>(changeEvent)
					};
					var serializedEventPointer = CreateSerializedEvent(aggregate.Id(), changeEvent, SerializeEvent(largeEventPointer), idempotencyMarkerOperation.RowKey);

					serializedEventPointer.EventType = _eventNameMapper.GetName<T>(largeEventPointer);

					batchOperation.Add(serializedEventPointer);
					largeChangeEvents.Add(eventEntity.RowKey, changeEvent);
				}
				else
					batchOperation.Add(eventEntity);
			}

			batchOperation.Add(idempotencyMarkerOperation, recordAt: 0);

			await SubmitBatchOperationsAsync(aggregate, idempotencyId, batchOperation, cancellationToken);

			if (largeChangeEvents.Count > 0)
			{
				// Always snapshot when there's a large event.
				shouldSnapshot = true;

				await WriteLargeEventEntitiesAsync(aggregate, [.. largeChangeEvents], idempotencyId, idempotencyMarkerOperation.RowKey, cancellationToken);
			}

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

	//static T CloneForNotification(T aggregate)
	//{
	//	T clonedAggregate;
	//	if (aggregate is ICloneable cloneable)
	//		clonedAggregate = (T)cloneable.Clone();
	//	else
	//		clonedAggregate = DeserializeSnapshot(SerializeSnapshot(aggregate));

	//	clonedAggregate.Details.Locked = true;

	//	return clonedAggregate;
	//}

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

	async Task WriteLargeEventEntitiesAsync(T aggregate, KeyValuePair<string, IEvent>[] largeChangeEvents, string idempotencyId, string compoundIdempotencyId, CancellationToken cancellationToken)
	{
		var aggregateId = aggregate.Id();
		for (var i = 0; i < largeChangeEvents.Length; i++)
		{
			var largeEvent = largeChangeEvents[i];
			var blobName = GenerateEventBlobName(aggregateId, largeEvent.Key);
			var largeEventContent = SerializeEvent(largeEvent.Value);
			using MemoryStream stream = new();
			using (StreamWriter writer = new(stream, Encoding.UTF8, _serializationBufferSize, true))
				await writer.WriteAsync(largeEventContent);

			stream.Position = 0;

			await _blobClient.UploadAsync(blobName, stream, metadata: new Dictionary<string, string> {
				{ "AggregateId", aggregateId },
				{ "EventType", _eventNameMapper.GetName<T>(largeEvent.Value) },
				{ "IdempotencyId", idempotencyId },
				{ "CompoundIdempotencyId", compoundIdempotencyId },
			}, overwrite: true, cancellationToken: cancellationToken);

			_eventStoreTelemetry.WritingLargeEvent(aggregateId, blobName, stream.Length, largeEvent.Value.GetType().FullName ?? largeEvent.Value.GetType().Name);
		}
	}

	static IdempotencyMarkerEntity CreateIdempotencyMarkerOperation(T aggregate, string idempotencyId, IEvent[] changeEvents)
	{
		var compoundIdempotencyId = GenerateIdempotencyId(idempotencyId, changeEvents);
		var rowKey = CreateIdempotencyCheckRowKey(compoundIdempotencyId);
		IdempotencyMarkerEntity marker = new(aggregate.Id(), rowKey);
		IdempotencyMarkerEventPayload eventObject = new()
		{
			EventIds = [.. changeEvents.Select(m => m.Details.AggregateVersion).OrderBy(m => m)]
		};

		marker.Events = JsonConvert.SerializeObject(eventObject, Formatting.None);

		return marker;
	}

	static string GenerateIdempotencyId(string idempotencyId, IEvent[] changeEvents)
	{
		HashCode hash = new();
		for (var i = 0; i < changeEvents.Length; i++)
		{
			var @event = changeEvents[i];
			hash.Add(@event);
		}

		return $"{idempotencyId}_{hash.ToHashCode()}";
	}

	async Task SubmitBatchOperationsAsync(T aggregate, string idempotencyId, BatchOperation batchOperation, CancellationToken cancellationToken)
	{
		try
		{
			// idx 0: IdempotencyMarker - Add
			// idx 1: StreamEntity - Add or Update (merge: false)
			// idx x: Events - Add

			var batchResults = await _tableClient.SubmitBatchAsync(batchOperation, cancellationToken);

			aggregate.Details.Etag = batchResults.Responses[1].Headers.ETag?.ToString();

			var currentVersion = aggregate.Details.CurrentVersion;

			aggregate.ClearUnsavedEvents();

			aggregate.Details.CurrentVersion = aggregate.Details.SavedVersion = currentVersion;
		}
		catch (RequestFailedException ex)
		{
			_eventStoreTelemetry.SaveFailedAtStorage(aggregate.Id(), _aggregateTypeFullName, ex.Status, ex);

			var statusCode = (HttpStatusCode)ex.Status;

			ClearCacheFireAndForget(aggregate);

			if (statusCode == HttpStatusCode.PreconditionFailed)
				throw new Exceptions.ConcurrencyException(aggregate.Id(), idempotencyId, aggregate.Details.CurrentVersion, aggregate.Details.SavedVersion);

			if (statusCode == HttpStatusCode.Conflict)
			{
				var errorEntity = batchOperation.FailedEntity;
				if (errorEntity != null)
				{
					if (errorEntity.RowKey.StartsWith(TableEventStoreConstants.IdempotencyCheckRowKeyPrefix, StringComparison.Ordinal))
						// Idempotency marker already exists, that means transaction with this idempotencyId already succeeded, so we don't care anymore
						return;

					if (errorEntity.RowKey.Equals(TableEventStoreConstants.StreamVersionRowKey, StringComparison.Ordinal))
						// Stream version Etag check or initial insert has failed, so somebody modified aggregate before us and whole transaction has to be retried.
						throw new Exceptions.ConcurrencyException(aggregate.Id(), idempotencyId, aggregate.Details.CurrentVersion, aggregate.Details.SavedVersion);
				}
			}

			throw new Exceptions.CommitException(ex.Status, aggregate.Id(), idempotencyId, aggregate.Details.CurrentVersion, aggregate.Details.SavedVersion, ex.Message);
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
		var snapshotName = GenerateSnapshotBlobName(aggregate.Id());

		using MemoryStream content = new();
		using (StreamWriter writer = new(content, Encoding.UTF8, _serializationBufferSize, leaveOpen: true))
			await writer.WriteAsync(snapshot);

		content.Position = 0;

		await _blobClient.UploadAsync(snapshotName, content, overwrite: true, cancellationToken: cancellationToken);
	}

	EventEntity CreateSerializedEvent(string aggregateId, IEvent @event, string serializedEvent, string compoundIdempotencyId)
		=> new()
		{
			PartitionKey = aggregateId,
			RowKey = CreateEventRowKey(@event.Details.AggregateVersion),
			Payload = serializedEvent,
			EventType = _eventNameMapper.GetName<T>(@event),
			IdempotencyId = compoundIdempotencyId
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
