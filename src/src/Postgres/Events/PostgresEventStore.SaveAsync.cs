using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using Npgsql;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Aggregates.Snapshotting;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.Postgres.Events.Exceptions;
using Purview.EventSourcing.Storage;
using Purview.EventSourcing.Validation;

namespace Purview.EventSourcing.Postgres.Events;

partial class PostgresEventStore<T>
{
	///<inheritdoc/>
	[DebuggerStepThrough]
	public async Task<SaveResult<T>> SaveAsync(
		[NotNull] T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		var operation = await SaveCoreAsync(aggregate, operationContext, null, null, cancellationToken);
		await operation.AfterCommitAsync(cancellationToken);
		return operation.Result;
	}

	string ITransactionalEventStore<T>.TransactionBoundaryKey =>
		new NpgsqlConnectionStringBuilder(_eventStoreOptions.Value.ConnectionString).ConnectionString;

	DbConnection ITransactionalEventStore<T>.CreateTransactionConnection() =>
		new NpgsqlConnection(_eventStoreOptions.Value.ConnectionString);

	Task ITransactionalEventStore<T>.EnsureTransactionConfiguredAsync(
		DbConnection connection,
		CancellationToken cancellationToken
	) => _client.EnsureTableExistsAsync(GetNpgsqlConnection(connection), cancellationToken);

	Task<TransactionalSaveOperation<T>> ITransactionalEventStore<T>.SaveInTransactionAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		DbConnection connection,
		DbTransaction transaction,
		CancellationToken cancellationToken
	) =>
		SaveCoreAsync(
			aggregate,
			operationContext,
			GetNpgsqlConnection(connection),
			GetNpgsqlTransaction(transaction),
			cancellationToken
		);

	async Task<TransactionalSaveOperation<T>> SaveCoreAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		NpgsqlConnection? connection,
		NpgsqlTransaction? transaction,
		CancellationToken cancellationToken,
		params IEvent[] additionalEvents
	)
	{
		operationContext ??= EventStoreOperationContext.DefaultContext();

		FulfilRequirements(aggregate);

		var idempotencyId = operationContext.CorrelationId ?? Activity.Current?.Id ?? $"{Guid.NewGuid()}";
		var validationResult = await GuardAsync(aggregate, cancellationToken);

		if (!validationResult.IsValid)
		{
			return new TransactionalSaveOperation<T>(
				SaveResultBuilder.Create(aggregate, false, false, validationResult)
			);
		}

		if (aggregate.Details.Locked)
		{
			return operationContext.LockMode is LockHandlingMode.ThrowsException
				? throw new AggregateLockedException(idempotencyId)
				: new TransactionalSaveOperation<T>(SaveResultBuilder.Create(aggregate, false, false));
		}

		if (string.IsNullOrWhiteSpace(aggregate.Details.Id))
			throw new MissingAggregateIdException(idempotencyId);

		_eventStoreTelemetry.SaveCalled(aggregate.Id(), _aggregateTypeFullName, aggregate.AggregateType);
		var activity = _eventStoreTelemetry.SaveAggregate(aggregate.Id(), _aggregateTypeFullName);

		if (!aggregate.HasUnsavedEvents() && (additionalEvents?.Length ?? 0) == 0)
		{
			_eventStoreTelemetry.SaveContainedNoChanges(
				aggregate.Id(),
				_aggregateTypeFullName,
				aggregate.AggregateType
			);
			activity?.Dispose();

			return new TransactionalSaveOperation<T>(SaveResultBuilder.Create(aggregate, false, true));
		}

		var isNew = aggregate.IsNew();
		var changeEvents = aggregate.GetUnsavedEvents().Concat((additionalEvents ?? []).AsEnumerable()).ToArray();

		if (changeEvents.Length > _eventStoreOptions.Value.MaxEventCountOnSave)
		{
			activity?.Dispose();
			throw new ArgumentOutOfRangeException(
				$"The maximum amount of events to save was exceeded. Attempted: {changeEvents.Length}, Maximum: {_eventStoreOptions.Value.MaxEventCountOnSave}"
			);
		}

		var idempotencyIdAsString = idempotencyId.ToUpperInvariant();
		var idempotencyMarkerId = CreateIdempotencyCheckId(aggregate.Id(), idempotencyIdAsString);

		if (operationContext.UseIdempotencyMarker)
		{
			try
			{
				var existing = connection is null
					? await _client.GetByIdAsync(idempotencyMarkerId, cancellationToken)
					: await _client.GetByIdAsync(idempotencyMarkerId, connection, transaction, cancellationToken);

				if (existing != null)
				{
					_eventStoreTelemetry.EventsAlreadyApplied(aggregate.Id(), idempotencyId);
					activity?.Dispose();
					return new TransactionalSaveOperation<T>(SaveResultBuilder.Create(aggregate, true, true));
				}
			}
#pragma warning disable CA1031
			catch (Exception ex)
#pragma warning restore CA1031
			{
				_eventStoreTelemetry.GetIdempotencyMarkerFailed(aggregate.Id(), idempotencyId, ex);
			}
		}

		return await PersistAndNotifyAsync(
			aggregate,
			operationContext,
			idempotencyId,
			changeEvents,
			isNew,
			connection,
			transaction,
			idempotencyIdAsString,
			idempotencyMarkerId,
			activity,
			cancellationToken
		);
	}

	async Task<TransactionalSaveOperation<T>> PersistAndNotifyAsync(
		T aggregate,
		EventStoreOperationContext operationContext,
		string idempotencyId,
		IEvent[] changeEvents,
		bool isNew,
		NpgsqlConnection? connection,
		NpgsqlTransaction? transaction,
		string idempotencyIdAsString,
		string idempotencyMarkerId,
		Activity? activity,
		CancellationToken cancellationToken
	)
	{
		if (
			operationContext.NotificationMode.HasFlag(NotificationModes.BeforeDelete)
			&& changeEvents.OfType<Deleted>().Any()
		)
			await _aggregateChangeNotifier.BeforeDeleteAsync(aggregate, cancellationToken);
		else if (operationContext.NotificationMode.HasFlag(NotificationModes.BeforeSave))
			await _aggregateChangeNotifier.BeforeSaveAsync(aggregate, isNew, cancellationToken);

		var streamEntity = await GetStreamVersionAsync(
			aggregate.Id(),
			!isNew,
			connection,
			transaction,
			cancellationToken
		);

		if (streamEntity?.IsDeleted == true)
		{
			var throwIfDeleted = !changeEvents.OfType<Restored>().Any();
			if (throwIfDeleted)
			{
				activity?.Dispose();
				throw new AggregateDeletedException(aggregate.Id(), idempotencyId);
			}
		}

		try
		{
			var previousAggregateVersion = aggregate.Details.SavedVersion;
			var shouldSnapshot = ShouldSnapShot(aggregate, changeEvents, operationContext);
			var now = DateTimeOffset.UtcNow;

			var streamVersionId = streamEntity?.Id ?? CreateStreamVersionId(aggregate.Id());
			PostgresEventStoreClient.RowData streamVersionRow = new()
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

			List<PostgresEventStoreClient.RowData> insertRows = [];
			for (var i = 0; i < changeEvents.Length; i++)
			{
				var changeEvent = changeEvents[i];

				changeEvent.Details.IdempotencyId = idempotencyIdAsString;
				changeEvent.Details.SchemaVersion = changeEvent.SchemaVersion;
				changeEvent.Details.UserId = userId;
				changeEvent.Details.CorrelationId ??= operationContext.CorrelationId;

				var serializedEvent = SerializeEvent(changeEvent);
				insertRows.Add(
					new PostgresEventStoreClient.RowData
					{
						Id = CreateEventId(aggregate.Id(), changeEvent.Details.AggregateVersion),
						EntityType = EventType,
						AggregateId = aggregate.Id(),
						AggregateType = aggregate.AggregateType,
						Version = changeEvent.Details.AggregateVersion,
						Payload = serializedEvent,
						EventType = _eventNameMapper.GetName<T>(changeEvent),
						IdempotencyId = idempotencyMarkerId,
						SchemaVersion = changeEvent.SchemaVersion,
						CorrelationId = changeEvent.Details.CorrelationId,
						CausationId = changeEvent.Details.CausationId,
						UserId = changeEvent.Details.UserId,
						Timestamp = now,
					}
				);
			}

			if (operationContext.UseIdempotencyMarker)
			{
				insertRows.Add(
					new PostgresEventStoreClient.RowData
					{
						Id = idempotencyMarkerId,
						EntityType = IdempotencyMarkerType,
						AggregateId = aggregate.Id(),
						AggregateType = aggregate.AggregateType,
						Version = 0,
						Timestamp = now,
					}
				);
			}

			await SubmitBatchOperationsAsync(
				aggregate,
				idempotencyId,
				streamVersionRow,
				insertRows,
				connection,
				transaction,
				cancellationToken
			);

			if (shouldSnapshot)
				await CreateSnapshotAsync(aggregate, connection, transaction, cancellationToken);

			var result = SaveResultBuilder.Create(aggregate, true, false);

			return new TransactionalSaveOperation<T>(
				result,
				async afterCommitCancellationToken =>
				{
					try
					{
						FinalizeSuccessfulSave(aggregate, shouldSnapshot);

						if (changeEvents.OfType<Deleted>().Any())
							_eventStoreTelemetry.AggregateDeleted(
								aggregate.Id(),
								_aggregateTypeFullName,
								aggregate.AggregateType
							);
						else if (changeEvents.OfType<Restored>().Any())
							_eventStoreTelemetry.AggregateRestored(
								aggregate.Id(),
								_aggregateTypeFullName,
								aggregate.AggregateType
							);

						_eventStoreTelemetry.SavedAggregate(
							aggregate.Id(),
							_aggregateTypeFullName,
							changeEvents.Length,
							aggregate.AggregateType
						);

						_eventStoreTelemetry.AggregateSaved(aggregate.AggregateType);
						_eventStoreTelemetry.SaveCompleted(activity, changeEvents.Length);

						await UpdateCacheAsync(aggregate, operationContext.CacheOptions, afterCommitCancellationToken);

						if (
							aggregate.Details.IsDeleted
							&& operationContext.NotificationMode.HasFlag(NotificationModes.AfterDelete)
						)
							await _aggregateChangeNotifier.AfterDeleteAsync(aggregate, afterCommitCancellationToken);
						else if (operationContext.NotificationMode.HasFlag(NotificationModes.AfterSave))
							await _aggregateChangeNotifier.AfterSaveAsync(
								aggregate,
								previousAggregateVersion,
								isNew,
								changeEvents,
								afterCommitCancellationToken
							);
					}
					finally
					{
						activity?.Dispose();
					}
				},
				_ =>
				{
					activity?.Dispose();
					return Task.CompletedTask;
				}
			);
		}
		catch (Exception ex)
		{
			activity?.Dispose();
			ClearCacheFireAndForget(aggregate);

			await HandleSaveFailureAsync(aggregate, operationContext, changeEvents, ex);

			throw;
		}
	}

	async Task HandleSaveFailureAsync(
		T aggregate,
		EventStoreOperationContext operationContext,
		IEvent[] changeEvents,
		Exception exception
	)
	{
		if (operationContext.NotificationMode.HasFlag(NotificationModes.OnFailure))
		{
			var deleteRequested = changeEvents.OfType<Deleted>().Any();
			await _aggregateChangeNotifier.FailureAsync(aggregate, deleteRequested, exception);
		}
	}

	async Task<ValidationResult> GuardAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		return _validator == null
			? await DefaultAggregateValidator<T>.Instance.ValidateAsync(aggregate, cancellationToken)
			: await _validator.ValidateAsync(aggregate, cancellationToken);
	}

	static bool ShouldSnapShot(T aggregate, IEvent[] events, EventStoreOperationContext context)
	{
		// Deleted/restored transitions must always be reflected promptly.
		if (aggregate.Details.IsDeleted || events.OfType<Restored>().Any())
			return true;

		// Default to a snapshot on every save (matching the historical behavior) while honoring
		// any per-operation strategy override or selector, so operators can reduce snapshot
		// write amplification on high-frequency aggregates.
		return SnapshotStrategyResolver.ShouldSnapshot(
			aggregate,
			events.Length,
			context,
			new IntervalSnapshotStrategy<T>()
		);
	}

	async Task SubmitBatchOperationsAsync(
		T aggregate,
		string idempotencyId,
		PostgresEventStoreClient.RowData streamVersionRow,
		List<PostgresEventStoreClient.RowData> insertRows,
		NpgsqlConnection? connection,
		NpgsqlTransaction? transaction,
		CancellationToken cancellationToken
	)
	{
		try
		{
			if (connection is null)
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
			}
			else
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
					connection,
					transaction!,
					cancellationToken
				);
			}
		}
		catch (PostgresException ex)
		{
			_eventStoreTelemetry.SaveFailedAtStorage(aggregate.Id(), _aggregateTypeFullName, ex);

			ClearCacheFireAndForget(aggregate);

			if (ex.SqlState is "23505")
			{
				throw new ConcurrencyException(
					aggregate.Id(),
					idempotencyId,
					aggregate.Details.CurrentVersion,
					aggregate.Details.SavedVersion
				);
			}

			throw new CommitException(
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

	async Task CreateSnapshotAsync(
		T aggregate,
		NpgsqlConnection? connection,
		NpgsqlTransaction? transaction,
		CancellationToken cancellationToken
	)
	{
		var previousSnapshotVersion = aggregate.Details.SnapshotVersion;

		try
		{
			aggregate.Details.SnapshotVersion = aggregate.Details.CurrentVersion;

			var snapshot = SerializeSnapshot(aggregate);
			var snapshotId = CreateSnapshotId(aggregate.Id());

			if (connection is null)
			{
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
					schemaVersion: _snapshotSchemaVersion,
					cancellationToken: cancellationToken
				);
			}
			else
			{
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
					connection,
					transaction!,
					schemaVersion: _snapshotSchemaVersion,
					cancellationToken: cancellationToken
				);
			}
		}
		finally
		{
			aggregate.Details.SnapshotVersion = previousSnapshotVersion;
		}
	}

	static void FinalizeSuccessfulSave(T aggregate, bool shouldSnapshot)
	{
		var currentVersion = aggregate.Details.CurrentVersion;
		aggregate.ClearUnsavedEvents();
		aggregate.Details.CurrentVersion = currentVersion;
		aggregate.Details.SavedVersion = currentVersion;
		aggregate.Details.Etag = currentVersion.ToString(CultureInfo.InvariantCulture);

		if (shouldSnapshot)
			aggregate.Details.SnapshotVersion = currentVersion;
	}

	void ClearCacheFireAndForget(T aggregate)
	{
		Task.Run(async () =>
		{
			try
			{
				var cacheKey = CreateCacheKey(aggregate.Id());
				await _distributedCache.RemoveAsync(cacheKey);
			}
#pragma warning disable CA1031
			catch (Exception ex)
#pragma warning restore CA1031
			{
				_eventStoreTelemetry.CacheRemovalFailure(aggregate.Id(), _aggregateTypeFullName, ex);
			}
		});
	}

	static NpgsqlConnection GetNpgsqlConnection(DbConnection connection) =>
		connection as NpgsqlConnection
		?? throw new InvalidOperationException("PostgreSQL transactions require a NpgsqlConnection.");

	static NpgsqlTransaction GetNpgsqlTransaction(DbTransaction transaction) =>
		transaction as NpgsqlTransaction
		?? throw new InvalidOperationException("PostgreSQL transactions require a NpgsqlTransaction.");
}
