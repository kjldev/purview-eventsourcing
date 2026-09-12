using System.Collections.Concurrent;
using System.Security.Claims;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Services;
using Purview.EventSourcing.Validation;

namespace Purview.EventSourcing.InMemory.Events;

/// <summary>
/// An in-memory <see cref="IEventStoreCore{T}"/> that stores aggregates and their events in process,
/// without queryable snapshot reads.
/// </summary>
/// <typeparam name="T">An <see cref="IAggregate"/> implementation.</typeparam>
/// <param name="aggregateChangeNotifier">The notifier invoked before and after aggregates are saved or deleted.</param>
/// <param name="aggregateRequirementsManager">The manager used to fulfil aggregate requirements.</param>
/// <param name="validator">Optional <see cref="IAggregateValidator{T}"/> used to validate aggregates before they are saved.</param>
/// <param name="aggregateIdFactory">Optional factory used to generate aggregate ids when none is supplied.</param>
/// <remarks>
/// Aggregates are held in memory and are identified by id in a case-insensitive manner. This store is
/// intended for testing and single-process scenarios; data is not shared between instances or persisted
/// across restarts.
/// </remarks>
/// <seealso cref="IInMemoryEventStore{T}"/>
public partial class InMemoryEventStore<T>(
	ChangeFeed.IAggregateChangeFeedNotifier<T> aggregateChangeNotifier,
	IAggregateRequirementsManager aggregateRequirementsManager,
	IAggregateValidator<T>? validator = null,
	IAggregateIdFactory? aggregateIdFactory = null
) : IInMemoryEventStore<T>, IDisposable
	where T : class, IAggregate, new()
{
	readonly ConcurrentDictionary<string, T> _aggregates = new(StringComparer.OrdinalIgnoreCase);
	readonly ConcurrentDictionary<string, ConcurrentDictionary<int, IEvent>> _events = new();

	readonly IAggregateValidator<T>? _validator = validator;

	/// <summary>
	/// The aggregates currently held by the store.
	/// </summary>
	/// <remarks>Exposed so derived snapshot stores can enumerate the in-memory aggregates.</remarks>
	protected IEnumerable<T> Aggregates => _aggregates.Values;

	///<inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_aggregates.Clear();
		_events.Clear();

		return Task.CompletedTask;
	}

	static Task<bool> ReturnAggregateAsync(bool isDeleted, string aggregateId, EventStoreOperationContext context)
	{
		if (isDeleted)
		{
#pragma warning disable IDE0010 // Add missing cases
			switch (context.DeleteMode)
			{
				case DeleteHandlingMode.ThrowsException:
					throw AggregateIsDeletedException(aggregateId);
				case DeleteHandlingMode.ReturnsNull:
					return Task.FromResult(false);
			}
#pragma warning restore IDE0010 // Add missing cases
		}

		return Task.FromResult(true);
	}

	///<inheritdoc/>
	public async Task<T> CreateAsync(string? aggregateId = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(aggregateId))
		{
			if (aggregateIdFactory != null)
			{
				aggregateId = await aggregateIdFactory.CreateAsync<T>(cancellationToken);
				if (string.IsNullOrWhiteSpace(aggregateId))
					throw new NullReferenceException(
						$"The {typeof(IAggregateIdFactory).FullName} implementation ({aggregateIdFactory.GetType().FullName}) generated a null or empty Id."
					);
			}
			else
				aggregateId = $"{Guid.NewGuid()}:D";
		}

		T aggregate = new() { Details = { Id = aggregateId } };

		return FulfilRequirements(aggregate);
	}

	///<inheritdoc/>
	public async Task<bool> DeleteAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		if (aggregate.Details.IsDeleted)
			throw AggregateIsDeletedException(aggregate.Id());

		operationContext ??= EventStoreOperationContext.DefaultContext();

		if (aggregate.IsNew())
			return false;

		if (operationContext.PermanentlyDelete)
		{
			RemoveFromCache(aggregate);
			return true;
		}

		Deleted deleteAggregateEvent = new()
		{
			Details = { AggregateVersion = aggregate.Details.CurrentVersion + 1, When = DateTimeOffset.UtcNow },
		};
		aggregate.ApplyEvent(deleteAggregateEvent);

		await AddToCacheAsync(aggregate, deleteAggregateEvent);

		return true;
	}

	void RemoveFromCache(T aggregate)
	{
		_aggregates.TryRemove(aggregate.Id(), out _);
		if (_events.TryRemove(aggregate.Id(), out var events))
			events.Clear();
	}

	Task<T> AddToCacheAsync(T aggregate, params IEvent[] additionalEvents) =>
		AddToCache(aggregate, true, additionalEvents);

	Task<T> AddToCache(T aggregate, bool fulfilRequirements, params IEvent[] additionalEvents)
	{
		var events = _events.GetOrAdd(aggregate.Id(), _ => new());
		foreach (var @event in aggregate.GetUnsavedEvents().Concat(additionalEvents ?? []))
		{
			// A conflicting version means another write already persisted this aggregate
			// version. Surface it as a concurrency conflict rather than silently dropping the
			// event, which would lose data.
			if (!events.TryAdd(@event.Details.AggregateVersion, @event))
				throw new Exceptions.ConcurrencyException(
					aggregate.Id(),
					@event.Details.IdempotencyId ?? $"{Guid.NewGuid()}",
					@event.Details.AggregateVersion,
					!events.IsEmpty ? events.Keys.Max() : aggregate.Details.SavedVersion
				);
		}

		aggregate.ClearUnsavedEvents();

		_aggregates.AddOrUpdate(aggregate.Id(), aggregate, (key, existingAggregate) => aggregate);

		if (fulfilRequirements)
			FulfilRequirements(aggregate);

		return Task.FromResult(aggregate);
	}

	///<inheritdoc/>
	public async Task<ExistsState> ExistsAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		await Task.CompletedTask;

		return _aggregates.TryGetValue(aggregateId, out var aggregate)
			? aggregate.Details.IsDeleted
				? ExistsState.ExistsInDeletedState with
				{
					Version = aggregate.Details.CurrentVersion,
				}
				: ExistsState.Exists with
				{
					Version = aggregate.Details.CurrentVersion,
				}
			: ExistsState.DoesNotExist;
	}

	///<inheritdoc/>
	public T FulfilRequirements(T aggregate)
	{
		aggregateRequirementsManager.Fulfil(aggregate);

		return aggregate;
	}

	///<inheritdoc/>
	public IAsyncEnumerable<string> GetAggregateIdsAsync(
		bool includeDeleted,
		CancellationToken cancellationToken = default
	)
	{
		var results = _aggregates.Values.AsEnumerable();
		if (!includeDeleted)
			results = results.Where(a => !a.Details.IsDeleted);

		return results.Select(a => a.Id()).ToAsyncEnumerable();
	}

	///<inheritdoc/>
	public async Task<T?> GetAsync(
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		operationContext ??= EventStoreOperationContext.DefaultContext();

		if (_aggregates.TryGetValue(aggregateId, out var aggregate))
		{
			if (!await ReturnAggregateAsync(aggregate.Details.IsDeleted, aggregateId, operationContext))
				return null;
		}

		aggregate ??= new T { Details = { Id = aggregateId } };
		return await AddToCacheAsync(aggregate);
	}

	///<inheritdoc/>
	public async Task<T?> GetAtAsync(
		string aggregateId,
		int version,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		var exists = await ExistsAsync(aggregateId, cancellationToken);
		if (!exists)
			return null;

		T aggregate = new() { Details = new() { Id = aggregateId } };

		var events = GetEventRangeAsync(aggregateId, 1, version, cancellationToken);
		await foreach (var @event in events.WithCancellation(cancellationToken))
			aggregate.ApplyEvent(@event.@event);

		return FulfilRequirements(aggregate);
	}

	///<inheritdoc/>
	public async Task<T?> GetDeletedAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		var aggregate = await GetAsync(
			aggregateId,
			new() { DeleteMode = DeleteHandlingMode.ReturnsAggregate },
			cancellationToken
		);

		return aggregate == null ? null
			: aggregate.Details.IsDeleted ? FulfilRequirements(aggregate)
			: throw AggregateNotDeletedException(aggregateId);
	}

	///<inheritdoc/>
	public IAsyncEnumerable<(IEvent @event, string eventType)> GetEventRangeAsync(
		string aggregateId,
		int versionFrom,
		int? versionTo,
		CancellationToken cancellationToken
	)
	{
		if (!_events.TryGetValue(aggregateId, out var eventList))
			return AsyncEnumerable.Empty<(IEvent @event, string eventType)>();

		// We have some events... so query and return.
		return eventList
			.Where(kvp => kvp.Key >= versionFrom && (!versionTo.HasValue || kvp.Key <= versionTo.Value))
			.OrderBy(kvp => kvp.Key)
			.Select(kvp => (kvp.Value, kvp.Value.GetType().Name))
			.ToAsyncEnumerable();
	}

	///<inheritdoc/>
	public async Task<T?> GetOrCreateAsync(
		string? aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		if (!string.IsNullOrWhiteSpace(aggregateId))
		{
			var exists = await ExistsAsync(aggregateId, cancellationToken);
			if (exists)
				return await GetAsync(aggregateId, operationContext, cancellationToken);
		}

		return await CreateAsync(aggregateId, cancellationToken);
	}

	///<inheritdoc/>
	public async Task<bool> IsDeletedAsync(string aggregateId, CancellationToken cancellationToken = default) =>
		await ExistsAsync(aggregateId, cancellationToken)
		&& (_aggregates.TryGetValue(aggregateId, out var aggregate) && aggregate.Details.IsDeleted);

	///<inheritdoc/>
	public async Task<bool> RestoreAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		if (aggregate == null)
			throw NullAggregate(aggregate);

		if (!aggregate.Details.IsDeleted)
			throw AggregateNotDeletedException(aggregate.Id());

		Restored restoreAggregateEvent = new()
		{
			Details = { AggregateVersion = aggregate.Details.CurrentVersion + 1, When = DateTimeOffset.UtcNow },
		};
		aggregate.ApplyEvent(restoreAggregateEvent);

		if (aggregate.IsNew())
			return false;

		await AddToCacheAsync(aggregate, restoreAggregateEvent);

		return true;
	}

	///<inheritdoc/>
	public async Task<SaveResult<T>> SaveAsync(
		T aggregate,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
	{
		operationContext ??= EventStoreOperationContext.DefaultContext();

		FulfilRequirements(aggregate);
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
				? throw AggregateLockedException(aggregate.Id())
				: ReturnSaveResult(aggregate, false, false);
		}

		if (string.IsNullOrWhiteSpace(aggregate.Details.Id))
			throw MissingAggregateIdException();

		if (!aggregate.HasUnsavedEvents())
			return ReturnSaveResult(aggregate, false, true);

		var isNew = aggregate.IsNew();
		var previousAggregateVersion = aggregate.Details.CurrentVersion;
		var changeEvents = aggregate.GetUnsavedEvents().ToArray();

		if (
			operationContext.NotificationMode.HasFlag(NotificationModes.BeforeDelete)
			&& changeEvents.OfType<Deleted>().Any()
		)
			await aggregateChangeNotifier.BeforeDeleteAsync(aggregate, cancellationToken);
		else if (operationContext.NotificationMode.HasFlag(NotificationModes.BeforeSave))
			await aggregateChangeNotifier.BeforeSaveAsync(aggregate, isNew, cancellationToken);

		if (await IsDeletedAsync(aggregate.Id(), cancellationToken))
		{
			var throwIfDeleted = !changeEvents.OfType<Restored>().Any();
			if (throwIfDeleted)
				throw AggregateIsDeletedException(aggregate.Id());
		}

		try
		{
			var userId = ClaimsPrincipal.Current?.FindFirst(operationContext.ClaimIdentifier)?.Value;
			if (operationContext.RequiresValidPrincipalIdentifier && string.IsNullOrWhiteSpace(userId))
				throw new NullReferenceException(
					$"Missing ClaimsPrincipal identifier '{operationContext.ClaimIdentifier}'. Unable to save aggregate."
				);

			await AddToCacheAsync(aggregate);

			// Mirror the persistence providers: after a successful save the aggregate must
			// reflect the persisted version so a subsequent mutation on the same instance
			// records events at the correct version.
			aggregate.Details.SavedVersion = aggregate.Details.CurrentVersion = previousAggregateVersion;

			if (aggregateChangeNotifier != null)
			{
				if (
					aggregate.Details.IsDeleted
					&& operationContext.NotificationMode.HasFlag(NotificationModes.AfterDelete)
				)
					await aggregateChangeNotifier.AfterDeleteAsync(aggregate, cancellationToken);
				else if (operationContext.NotificationMode.HasFlag(NotificationModes.AfterSave))
					await aggregateChangeNotifier.AfterSaveAsync(
						aggregate,
						previousAggregateVersion,
						isNew,
						changeEvents,
						cancellationToken
					);
			}
		}
		catch (Exception ex)
		{
			RemoveFromCache(aggregate);

			if (operationContext.NotificationMode.HasFlag(NotificationModes.OnFailure))
			{
				var deleteRequested = changeEvents.OfType<Deleted>().Any();
				await aggregateChangeNotifier.FailureAsync(aggregate, deleteRequested, ex, cancellationToken);
			}

			throw;
		}

		return ReturnSaveResult(aggregate, true, false);
	}

	/// <summary>
	/// Releases the aggregates and events held by the store.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release managed resources; otherwise, <see langword="false"/>.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_aggregates.Clear();
			_events.Clear();
		}
	}

	///<inheritdoc/>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	async Task<ValidationResult> GuardAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		return _validator == null
			? await DefaultAggregateValidator<T>.Instance.ValidateAsync(aggregate, cancellationToken)
			: await _validator.ValidateAsync(aggregate, cancellationToken);
	}
}
