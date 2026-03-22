using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using FluentValidation.Results;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.SqlServer;

partial class SqlServerEventStore<T>
{
	[DebuggerStepThrough]
	public Task<SaveResult<T>> SaveAsync(
		[NotNull] T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	) => SaveCoreAsync(aggregate, operationContext, cancellationToken);

	async Task<SaveResult<T>> SaveCoreAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken,
		params IEvent[] additionalEvents
	)
	{
		operationContext ??= EventStoreOperationContext.DefaultContext;

		FulfilRequirements(aggregate);

		var idempotencyId = operationContext.CorrelationId ?? Activity.Current?.Id ?? $"{Guid.NewGuid()}";
		var validationResult = await GuardAsync(aggregate, cancellationToken);

		static SaveResult<T> ReturnSaveResult(
			T a,
			bool success,
			bool skipped,
			ValidationResult? validationResult = null
		) => new(a, validationResult ?? new ValidationResult(), success, skipped);

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
		using var activity = _eventStoreTelemetry.SaveAggregate(aggregate.Id(), _aggregateTypeFullName);
		if (!aggregate.HasUnsavedEvents() && (additionalEvents?.Length ?? 0) == 0)
		{
			_eventStoreTelemetry.SaveContainedNoChanges(
				aggregate.Id(),
				_aggregateTypeFullName,
				aggregate.AggregateType
			);

			return ReturnSaveResult(aggregate, false, true);
		}

		var isNew = aggregate.IsNew();
		var changeEvents = aggregate.GetUnsavedEvents().Concat((additionalEvents ?? []).AsEnumerable()).ToArray();

		if (changeEvents.Length > _eventStoreOptions.Value.MaxEventCountOnSave)
			throw new ArgumentOutOfRangeException(
				$"The maximum amount of events to save was exceeded. Attempted: {changeEvents.Length}, Maximum: {_eventStoreOptions.Value.MaxEventCountOnSave}"
			);

		var idempotencyIdAsString = idempotencyId.ToUpperInvariant();
		var idempotencyMarkerId = CreateIdempotencyCheckId(aggregate.Id(), idempotencyIdAsString);

		if (operationContext.UseIdempotencyMarker)
		{
			try
			{
				var existing = await _client.GetByIdAsync(idempotencyMarkerId, cancellationToken);
				if (existing != null)
				{
					_eventStoreTelemetry.EventsAlreadyApplied(aggregate.Id(), idempotencyId);
					return ReturnSaveResult(aggregate, true, true);
				}
			}
			catch (Exception ex)
			{
				_eventStoreTelemetry.GetIdempotencyMarkerFailed(aggregate.Id(), idempotencyId, ex);
			}
		}

		if (
			operationContext.NotificationMode.HasFlag(NotificationModes.BeforeDelete)
			&& changeEvents.OfType<DeleteEvent>().Any()
		)
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

			var now = DateTimeOffset.UtcNow;

			// Build stream version row
			var streamVersionId = streamEntity?.Id ?? CreateStreamVersionId(aggregate.Id());
			var streamVersionRow = new SqlServerEventStoreClient.RowData
			{
				Id = streamVersionId,
				EntityType = StreamVersionType,
				AggregateId = aggregate.Id(),
				AggregateType = aggregate.AggregateType,
				Version = aggregate.Details.CurrentVersion,
				IsDeleted = aggregate.Details.IsDeleted,
				Timestamp = now,
			};

			var userId = ClaimsPrincipal.Current?.FindFirst(operationContext.ClaimIdentifier)?.Value;
			if (operationContext.RequiresValidPrincipalIdentifier && string.IsNullOrWhiteSpace(userId))
				throw new NullReferenceException(
					$"Missing ClaimsPrincipal identifier '{operationContext.ClaimIdentifier}'. Unable to save aggregate."
				);

			// Build event rows and idempotency marker
			List<SqlServerEventStoreClient.RowData> insertRows = [];
			for (var i = 0; i < changeEvents.Length; i++)
			{
				var changeEvent = changeEvents[i];

				changeEvent.Details.IdempotencyId = idempotencyIdAsString;
				changeEvent.Details.UserId = userId;

				var serializedEvent = SerializeEvent(changeEvent);
				insertRows.Add(
					new SqlServerEventStoreClient.RowData
					{
						Id = CreateEventId(aggregate.Id(), changeEvent.Details.AggregateVersion),
						EntityType = EventType,
						AggregateId = aggregate.Id(),
						AggregateType = aggregate.AggregateType,
						Version = changeEvent.Details.AggregateVersion,
						Payload = serializedEvent,
						EventType = _eventNameMapper.GetName<T>(changeEvent),
						IdempotencyId = idempotencyMarkerId,
						Timestamp = now,
					}
				);
			}

			// Idempotency marker row
			if (operationContext.UseIdempotencyMarker)
				insertRows.Add(
					new SqlServerEventStoreClient.RowData
					{
						Id = idempotencyMarkerId,
						EntityType = IdempotencyMarkerType,
						AggregateId = aggregate.Id(),
						AggregateType = aggregate.AggregateType,
						Version = 0,
						Timestamp = now,
					}
				);

			await SubmitBatchOperationsAsync(aggregate, idempotencyId, streamVersionRow, insertRows, cancellationToken);

			if (shouldSnapshot)
				await CreateSnapshotAsync(aggregate, cancellationToken);

			if (changeEvents.OfType<DeleteEvent>().Any())
				_eventStoreTelemetry.AggregateDeleted(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);
			else if (changeEvents.OfType<RestoreEvent>().Any())
				_eventStoreTelemetry.AggregateRestored(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);

			_eventStoreTelemetry.SavedAggregate(
				aggregate.Id(),
				_aggregateTypeFullName,
				changeEvents.Length,
				aggregate.AggregateType
			);

			_eventStoreTelemetry.AggregateSaved(aggregate.AggregateType);
			_eventStoreTelemetry.SaveCompleted(activity, changeEvents.Length);

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

		var validationResult =
			_validator == null
				? await DefaultAggregateValidator<T>.Instance.ValidateAsync(aggregate, cancellationToken)
				: await _validator.ValidateAsync(aggregate, cancellationToken);

		return validationResult;
	}

	bool ShouldSnapShot(T aggregate, IEvent[] events)
	{
		if (aggregate.Details.IsDeleted || events.OfType<RestoreEvent>().Any())
			return true;

		return (aggregate.Details.CurrentVersion - aggregate.Details.SnapshotVersion)
			>= _eventStoreOptions.Value.SnapshotInterval;
	}

	async Task SubmitBatchOperationsAsync(
		T aggregate,
		string idempotencyId,
		SqlServerEventStoreClient.RowData streamVersionRow,
		List<SqlServerEventStoreClient.RowData> insertRows,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await _client.UpsertWithBatchAsync(
				streamVersionRow.Id,
				streamVersionRow.EntityType,
				streamVersionRow.AggregateId,
				streamVersionRow.AggregateType,
				streamVersionRow.Version,
				streamVersionRow.IsDeleted,
				streamVersionRow.Payload,
				streamVersionRow.EventType,
				streamVersionRow.IdempotencyId,
				streamVersionRow.Timestamp,
				insertRows,
				cancellationToken
			);

			var currentVersion = aggregate.Details.CurrentVersion;

			aggregate.ClearUnsavedEvents();

			aggregate.Details.CurrentVersion = aggregate.Details.SavedVersion = currentVersion;
			aggregate.Details.Etag = currentVersion.ToString(CultureInfo.InvariantCulture);
		}
		catch (Microsoft.Data.SqlClient.SqlException ex)
		{
			_eventStoreTelemetry.SaveFailedAtStorage(aggregate.Id(), _aggregateTypeFullName, ex);

			ClearCacheFireAndForget(aggregate);

			throw new Exceptions.CommitException(
				aggregate.Id(),
				idempotencyId,
				aggregate.Details.CurrentVersion,
				aggregate.Details.SavedVersion,
				ex
			);
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
		aggregate.Details.SnapshotVersion = aggregate.Details.CurrentVersion;

		var snapshot = SerializeSnapshot(aggregate);
		var snapshotId = CreateSnapshotId(aggregate.Id());

		await _client.UpsertAsync(
			snapshotId,
			SnapshotType,
			aggregate.Id(),
			_aggregateTypeShortName,
			aggregate.Details.CurrentVersion,
			aggregate.Details.IsDeleted,
			snapshot,
			null,
			null,
			DateTimeOffset.UtcNow,
			cancellationToken
		);
	}

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
