using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.Aggregates.Exceptions;

namespace Purview.EventSourcing.Aggregates;

/// <summary>
/// A default base class for an <see cref="IAggregate"/> implementation.
/// </summary>
public abstract class AggregateBase : IAggregate
{
	readonly Dictionary<Type, Action<IEvent>> _appliersByEventType = [];

	ConcurrentBag<IEvent> _unsavedEvents = [];

	/// <summary>
	/// Initializes the base-class.
	/// </summary>
	/// <param name="aggregateType">Specifies the <see cref="AggregateType"/>, if not value is specified
	/// <see cref="TypeNameHelper.GetName(Type, string, bool)"/> is called, with 'Aggregate' being trimmed if it's a suffix
	/// of the type na,e.</param>
	protected AggregateBase(string? aggregateType = null)
	{
		AggregateType = aggregateType ?? TypeNameHelper.GetName(GetType(), "Aggregate");

		RegisterSystemEvents();
#pragma warning disable CA2214 // Do not call overridable methods in constructors
		RegisterEvents();
#pragma warning restore CA2214 // Do not call overridable methods in constructors
	}

	///<inheritdoc/>
	public string AggregateType { get; }

	///<inheritdoc/>
	public AggregateDetails Details { get; init; } = new();

	void RegisterSystemEvents()
	{
		Register<DeleteEvent>(_ => Details.IsDeleted = true);
		Register<RestoreEvent>(_ => Details.IsDeleted = false);
		Register<ForceSaveEvent>(_ => { });
	}

	/// <summary>
	/// Used to register custom <see cref="IEvent"/>
	/// implementations using the <see cref="Register{TEvent}(Action{TEvent})"/>. method.
	/// </summary>
	protected abstract void RegisterEvents();

	///<inheritdoc/>
	public virtual void ClearUnsavedEvents(int? upToVersion = null)
	{
		var unsavedEventCount = _unsavedEvents.Count;
		if (upToVersion.HasValue)
			_unsavedEvents = [.. _unsavedEvents.Where(m => m.Details.AggregateVersion > upToVersion)];
		else
			_unsavedEvents.Clear();

		unsavedEventCount -= _unsavedEvents.Count;

		Details.CurrentVersion -= unsavedEventCount;
	}

	///<inheritdoc/>
	public IEnumerable<IEvent> GetUnsavedEvents() => [.. _unsavedEvents];

	///<inheritdoc/>
	public bool HasUnsavedEvents() => !_unsavedEvents.IsEmpty;

	///<inheritdoc/>
	public bool CanApplyEvent([NotNull] IEvent aggregateEvent)
		=> _appliersByEventType.ContainsKey(aggregateEvent.GetType());

	/// <summary>
	/// Records an <see cref="IEvent"/> in the form of <typeparamref name="TEvent"/>.
	/// and stores the record ready for saving via the <see cref="IEventStore{T}.SaveAsync(T, EventStoreOperationContext?, CancellationToken)"/>
	/// method. The event is also applied (via <see cref="IAggregate.ApplyEvent(IEvent)"/>) once it's been recorded.
	/// </summary>
	/// <typeparam name="TEvent">The <see cref="IEvent"/> implementation type.</typeparam>
	/// <param name="event">The event to save.</param>
	/// <returns>The current <see cref="AggregateBase"/> instance.</returns>
	/// <remarks>The <see cref="EventDetails.AggregateVersion"/> and <see cref="EventDetails.When"/>
	/// of the <see cref="IEvent.Details"/> property are updated during this operation.</remarks>
	protected internal AggregateBase RecordAndApply<TEvent>(TEvent @event)
		where TEvent : IEvent
	{
		ArgumentNullException.ThrowIfNull(@event, nameof(@event));

		if (@event.Details == null)
			throw new NullReferenceException($"The {nameof(IEvent.Details)} is null.");

		if (!_appliersByEventType.ContainsKey(@event.GetType()))
			throw new UnregisteredEventException(@event.GetType(), this);

		if (Details.Locked)
			throw new LockedException(Details.Id);

		@event.Details.AggregateVersion = Details.CurrentVersion + 1;
		@event.Details.When = DateTimeOffset.UtcNow;

		_unsavedEvents.Add(@event);

		((IAggregate)this).ApplyEvent(@event);

		return this;
	}

	/// <summary>
	/// Applies the <see cref="ForceSaveEvent"/> to the aggregate,
	/// allowing the aggregate to be saved, regardless of other operations.
	/// </summary>
	/// <remarks>This is only applied if <see cref="HasUnsavedEvents"/> returns false. This can be useful for situations where
	/// you need to re-populate a queryable store for example.</remarks>
	public void ForceSave()
	{
		if (!HasUnsavedEvents())
			RecordAndApply(new ForceSaveEvent());
	}

	/// <summary>
	/// Registers an <see cref="Action{T}"/> as the handler to apply
	/// an <see cref="IEvent"/>.
	/// </summary>
	/// <typeparam name="TEvent">The <see cref="IEvent"/> type to register.</typeparam>
	/// <param name="applier">The action used to apply the event.</param>
	/// <remarks>The <typeparamref name="TEvent"/> <see cref="System.Reflection.MemberInfo.Name"/> of the event <see cref="Type"/>
	/// must end with 'Event'.</remarks>
	protected void Register<TEvent>(Action<TEvent> applier)
		where TEvent : IEvent
	{
		if (applier == null)
			throw new ArgumentNullException(nameof(applier));

		if (!typeof(TEvent).Name.EndsWith("Event", StringComparison.InvariantCulture))
			throw new InvalidOperationException($"Registering events failed, events must end with name 'Event'.\n\nFailed even type: {typeof(TEvent).FullName}");

		_appliersByEventType.Add(typeof(TEvent), ev => applier((TEvent)ev));
	}

#pragma warning disable CA1033 // Interface methods should be callable by child types

	///<inheritdoc/>
	IEnumerable<Type> IAggregate.GetRegisteredEventTypes() => _appliersByEventType.Keys;

	///<inheritdoc/>
	void IAggregate.ApplyEvent(IEvent @event)
	{
		if (Details.Locked)
			throw new LockedException(Details.Id);

		var eventApplier = _appliersByEventType[@event.GetType()];
		eventApplier(@event);

		if (@event.Details.AggregateVersion == 1)
			Details.Created = @event.Details.When;

		Details.Updated = @event.Details.When;
		Details.CurrentVersion = @event.Details.AggregateVersion;
	}

#pragma warning restore CA1033 // Interface methods should be callable by child types
}
