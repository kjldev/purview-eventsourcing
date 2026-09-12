using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing;

/// <summary>
/// Convenience extension methods over <see cref="IEventStore"/> for common create, get, save, delete,
/// restore, and transaction workflows.
/// </summary>
/// <remarks>
/// These helpers reduce boilerplate by supplying default operation contexts, converting object-based ids to
/// strings, running creator callbacks on newly created aggregates, and offering shorter transaction
/// enlistment forms. They are hidden from IntelliSense as they are intended for use through the main
/// <see cref="IEventStore"/> and <see cref="IQueryableEventStore"/> facades.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[System.Diagnostics.DebuggerStepThrough]
public static class IEventStoreExtensions
{
	#region QuickCreate/ QuickCreateAsync

	/// <summary>
	/// Creates a new <typeparamref name="T"/>, but will not call <see cref="IAggregateIdFactory.CreateAsync{T}(CancellationToken)"/>
	/// to create a new Id. It will take the <paramref name="aggregateId"/> parameter, or the id parameter is null or empty
	/// use a new lowered <see cref="Guid"/>.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use, or null with either the specified or a generated id.</param>
	/// <returns>A new aggregate of <typeparamref name="T"/>.</returns>
	/// <remarks>Calls <see cref="IEventStore.FulfilRequirements{T}(T)"/> to apply any requirements.</remarks>
	public static T QuickCreate<T>([NotNull] this IEventStore eventStore, string? aggregateId = null)
		where T : class, IAggregate, new()
	{
		if (string.IsNullOrWhiteSpace(aggregateId))
			aggregateId = $"{Guid.NewGuid()}:D";

		T aggregate = new() { Details = new() { Id = aggregateId } };

		eventStore.FulfilRequirements(aggregate);

		return aggregate;
	}

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> using the string representation of the id.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use; when null, a new <see cref="Guid"/> is generated.</param>
	/// <returns>A new aggregate of <typeparamref name="T"/>.</returns>
	public static T QuickCreate<T>(this IEventStore eventStore, object? aggregateId)
		where T : class, IAggregate, new() => eventStore.QuickCreate<T>(aggregateId?.ToString());

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> and runs the <paramref name="creator"/> callback
	/// against it.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use; when null, a new <see cref="Guid"/> is generated.</param>
	/// <param name="creator">The callback used to initialize the new aggregate.</param>
	/// <returns>The initialized aggregate.</returns>
	public static T QuickCreate<T>(this IEventStore eventStore, string? aggregateId, [NotNull] Action<T> creator)
		where T : class, IAggregate, new()
	{
		var aggregate = eventStore.QuickCreate<T>(aggregateId);

		creator(aggregate);

		return aggregate;
	}

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> using the string representation of the id and runs
	/// the <paramref name="creator"/> callback against it.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use; when null, a new <see cref="Guid"/> is generated.</param>
	/// <param name="creator">The callback used to initialize the new aggregate.</param>
	/// <returns>The initialized aggregate.</returns>
	public static T QuickCreate<T>(this IEventStore eventStore, object? aggregateId, Action<T> creator)
		where T : class, IAggregate, new() => eventStore.QuickCreate(aggregateId?.ToString(), creator);

	/// <summary>
	/// Asynchronously creates a new aggregate of <typeparamref name="T"/> and runs the <paramref name="creator"/>
	/// callback against it.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use; when null, a new <see cref="Guid"/> is generated.</param>
	/// <param name="creator">The asynchronous callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The initialized aggregate.</returns>
	public static async Task<T> QuickCreateAsync<T>(
		this IEventStore eventStore,
		string? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = eventStore.QuickCreate<T>(aggregateId);

		await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Asynchronously creates a new aggregate of <typeparamref name="T"/> using the string representation of the
	/// id and runs the <paramref name="creator"/> callback against it.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="aggregateId">The id to use; when null, a new <see cref="Guid"/> is generated.</param>
	/// <param name="creator">The asynchronous callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The initialized aggregate.</returns>
	public static async Task<T> QuickCreateAsync<T>(
		this IEventStore eventStore,
		object? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = eventStore.QuickCreate<T>(aggregateId?.ToString());

		await creator(aggregate, cancellationToken);

		return aggregate;
	}

	#endregion QuickCreate/ QuickCreateAsync

	#region GetOrCreateAsync

	#region id: string, with context.

	/// <summary>
	/// Gets the aggregate for the id, creating and initializing it with the <paramref name="creator"/> when it
	/// does not yet exist.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The asynchronous callback used to initialize a newly created aggregate.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId, context, cancellationToken);
		if (aggregate?.IsNew() == true)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Gets the aggregate for the id, creating and initializing it with the <paramref name="creator"/> when it
	/// does not yet exist.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The callback used to initialize a newly created aggregate.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId,
		[NotNull] Action<T> creator,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId, context, cancellationToken);
		if (aggregate?.IsNew() == true)
			creator(aggregate);

		return aggregate;
	}

	#endregion id: string, with context.

	#region id: string, without context.

	/// <summary>
	/// Gets the aggregate for the id, creating it when it does not yet exist using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.GetOrCreateAsync<T>(aggregateId, null, cancellationToken);

	/// <summary>
	/// Gets the aggregate for the id, creating and initializing it with the <paramref name="creator"/> when it
	/// does not yet exist using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The asynchronous callback used to initialize a newly created aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId, null, cancellationToken);
		if (aggregate?.IsNew() == true)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Gets the aggregate for the id, creating and initializing it with the <paramref name="creator"/> when it
	/// does not yet exist using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The callback used to initialize a newly created aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId,
		[NotNull] Action<T> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId, null, cancellationToken);
		if (aggregate?.IsNew() == true)
			creator(aggregate);

		return aggregate;
	}

	#endregion id: string, without context.

	#region id: object, with context.

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating it when it does not yet exist.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="operationContext">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetOrCreateAsync<T>(aggregateId?.ToString(), operationContext, cancellationToken);

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating and initializing it with the
	/// <paramref name="creator"/> when it does not yet exist.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The asynchronous callback used to initialize a newly created aggregate.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId?.ToString(), context, cancellationToken);
		if (aggregate?.IsNew() == true)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating and initializing it with the
	/// <paramref name="creator"/> when it does not yet exist.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The callback used to initialize a newly created aggregate.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId,
		[NotNull] Action<T> creator,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId?.ToString(), context, cancellationToken);
		if (aggregate?.IsNew() == true)
			creator(aggregate);

		return aggregate;
	}

	#endregion id: object, with context.

	#region id: object, no context.

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating it when it does not yet exist
	/// using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() =>
		eventStore.GetOrCreateAsync<T>(aggregateId?.ToString(), null, cancellationToken);

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating and initializing it with the
	/// <paramref name="creator"/> when it does not yet exist using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The asynchronous callback used to initialize a newly created aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId,
		[NotNull] Func<T, CancellationToken, Task> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(aggregateId?.ToString(), null, cancellationToken);
		if (aggregate?.IsNew() == true)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Gets the aggregate for the string representation of the id, creating and initializing it with the
	/// <paramref name="creator"/> when it does not yet exist using the default context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="id">The id of the aggregate to get, or use as the id of the aggregate to create.</param>
	/// <param name="creator">The callback used to initialize a newly created aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The existing or newly created aggregate, or null.</returns>
	public static async Task<T?> GetOrCreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? id,
		[NotNull] Action<T> creator,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.GetOrCreateAsync<T>(id?.ToString(), null, cancellationToken);
		if (aggregate?.IsNew() == true)
			creator(aggregate);

		return aggregate;
	}

	#endregion id: object, no context.

	#endregion GetOrCreateAsync

	#region CreateAsync

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> and optionally runs the <paramref name="creator"/>
	/// callback against it.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">Optional, the id of the aggregate to create; when null, an id is generated.</param>
	/// <param name="creator">Optional asynchronous callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The newly created aggregate.</returns>
	public static async Task<T> CreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId = null,
		Func<T, CancellationToken, Task>? creator = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.CreateAsync<T>(aggregateId, cancellationToken);
		if (creator != null)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> and optionally runs the <paramref name="creator"/>
	/// callback against it.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">Optional, the id of the aggregate to create; when null, an id is generated.</param>
	/// <param name="creator">Optional callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The newly created aggregate.</returns>
	public static async Task<T> CreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		string? aggregateId = null,
		Action<T>? creator = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.CreateAsync<T>(aggregateId, cancellationToken);
		creator?.Invoke(aggregate);

		return aggregate;
	}

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> using the string representation of the id.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">Optional, the id of the aggregate to create; when null, an id is generated.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The newly created aggregate.</returns>
	public static Task<T> CreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.CreateAsync<T>(aggregateId?.ToString(), cancellationToken);

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> using the string representation of the id and
	/// optionally runs the <paramref name="creator"/> callback against it.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">Optional, the id of the aggregate to create; when null, an id is generated.</param>
	/// <param name="creator">Optional asynchronous callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The newly created aggregate.</returns>
	public static async Task<T> CreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId = null,
		Func<T, CancellationToken, Task>? creator = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.CreateAsync<T>(aggregateId?.ToString(), cancellationToken);
		if (creator != null)
			await creator(aggregate, cancellationToken);

		return aggregate;
	}

	/// <summary>
	/// Creates a new aggregate of <typeparamref name="T"/> using the string representation of the id and
	/// optionally runs the <paramref name="creator"/> callback against it.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> to create.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">Optional, the id of the aggregate to create; when null, an id is generated.</param>
	/// <param name="creator">Optional callback used to initialize the new aggregate.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The newly created aggregate.</returns>
	public static async Task<T> CreateAsync<T>(
		[NotNull] this IEventStore eventStore,
		object? aggregateId = null,
		Action<T>? creator = null,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var aggregate = await eventStore.CreateAsync<T>(aggregateId?.ToString(), cancellationToken);
		creator?.Invoke(aggregate);

		return aggregate;
	}

	#endregion CreateAsync

	#region GetAsync

	/// <summary>
	/// Gets the aggregate for the id using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	public static Task<T?> GetAsync<T>(
		[NotNull] this IEventStore eventStore,
		string aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.GetAsync<T>(aggregateId, null, cancellationToken);

	/// <summary>
	/// Gets the aggregate for the string representation of the id using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<T?> GetAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.GetAsync<T>(idAsString, null, cancellationToken);
	}

	/// <summary>
	/// Gets the aggregate for the string representation of the id using the supplied operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<T?> GetAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.GetAsync<T>(idAsString, context, cancellationToken);
	}

	#endregion GetAsync

	#region GetAtAsync

	/// <summary>
	/// Gets the aggregate up to a specific version using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="version">The version of the aggregate to get.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	public static Task<T?> GetAtAsync<T>(
		[NotNull] this IEventStore eventStore,
		string aggregateId,
		int version,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.GetAtAsync<T>(aggregateId, version, null, cancellationToken);

	/// <summary>
	/// Gets the aggregate up to a specific version using the string representation of the id.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="version">The version of the aggregate to get.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	public static Task<T?> GetAtAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		int version,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.GetAtAsync<T>(aggregateId, version, null, cancellationToken);

	/// <summary>
	/// Gets the aggregate up to a specific version using the string representation of the id and the supplied
	/// operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to get.</param>
	/// <param name="version">The version of the aggregate to get.</param>
	/// <param name="context">The operational context controlling how the aggregate is retrieved.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The requested aggregate, or null when it does not exist.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<T?> GetAtAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		int version,
		EventStoreOperationContext? context,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.GetAtAsync<T>(idAsString, version, context, cancellationToken);
	}

	#endregion GetAtAsync

	#region IsDeletedAsync

	/// <summary>
	/// Determines if the aggregate with the string representation of the id exists in the deleted state.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to check.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>True when the aggregate exists in the deleted state, otherwise false.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<bool> IsDeletedAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.IsDeletedAsync<T>(idAsString, cancellationToken);
	}

	#endregion IsDeletedAsync

	#region GetDeletedAsync

	/// <summary>
	/// Gets a deleted aggregate using the string representation of the id.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the deleted aggregate to get.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>The deleted aggregate, or null when it is not found.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<T?> GetDeletedAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.GetDeletedAsync<T>(idAsString, cancellationToken);
	}

	#endregion GetDeletedAsync

	#region ExistsAsync

	/// <summary>
	/// Determines if the aggregate with the string representation of the id exists, including deleted states.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to check.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>An <see cref="ExistsState"/> describing the existence of the aggregate.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> has no string representation.</exception>
	public static Task<ExistsState> ExistsAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.ExistsAsync<T>(idAsString, cancellationToken);
	}

	/// <summary>
	/// Determines if the aggregate with the string representation of the id exists, returning
	/// <see cref="ExistsState.DoesNotExist"/> for null or blank ids instead of throwing.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to check.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>An <see cref="ExistsState"/> describing the existence of the aggregate.</returns>
	public static Task<ExistsState> ExistsWithNullCheckAsync<T>(
		[NotNull] this IEventStore eventStore,
		object aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		var idAsString = aggregateId?.ToString();
		ArgumentException.ThrowIfNullOrWhiteSpace(idAsString, nameof(aggregateId));

		return eventStore.ExistsWithNullCheckAsync<T>(idAsString, cancellationToken);
	}

	/// <summary>
	/// Determines if the aggregate exists, returning <see cref="ExistsState.DoesNotExist"/> for null or blank
	/// ids instead of throwing.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to check.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>An <see cref="ExistsState"/> describing the existence of the aggregate.</returns>
	public static Task<ExistsState> ExistsWithNullCheckAsync<T>(
		[NotNull] this IEventStore eventStore,
		string aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		return string.IsNullOrWhiteSpace(aggregateId)
			? Task.FromResult(ExistsState.DoesNotExist)
			: eventStore.ExistsAsync<T>(aggregateId, cancellationToken);
	}

	#endregion ExistsAsync

	#region Enlist

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> with no enlisted aggregates.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	public static IEventStoreTransaction Enlist([NotNull] this IEventStore eventStore)
	{
		ArgumentNullException.ThrowIfNull(eventStore);

		return new EventStoreTransaction();
	}

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> with the given <paramref name="correlationId"/>
	/// and no enlisted aggregates.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="correlationId">
	/// Optional correlation ID to bind all events together. When <see langword="null"/>, a new GUID is generated.
	/// </param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	public static IEventStoreTransaction Enlist([NotNull] this IEventStore eventStore, string? correlationId)
	{
		ArgumentNullException.ThrowIfNull(eventStore);

		return new EventStoreTransaction(correlationId);
	}

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> with the given <paramref name="operationContext"/>
	/// and no enlisted aggregates.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="operationContext">
	/// Optional <see cref="EventStoreOperationContext"/> applied to every aggregate save.
	/// When <see langword="null"/>, the default context is used.
	/// </param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	public static IEventStoreTransaction Enlist(
		[NotNull] this IEventStore eventStore,
		EventStoreOperationContext? operationContext
	)
	{
		ArgumentNullException.ThrowIfNull(eventStore);

		return new EventStoreTransaction(operationContext?.CorrelationId);
	}

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> and enlists all <paramref name="aggregates"/>
	/// against this event store, using an auto-generated correlation ID.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> responsible for persisting each aggregate.</param>
	/// <param name="aggregates">The aggregates to include in the transaction.</param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	/// <remarks>Call <see cref="IEventStoreTransaction.CommitAsync"/> to persist all enlisted aggregates.</remarks>
	public static IEventStoreTransaction Enlist<T>([NotNull] this IEventStore eventStore, params T[] aggregates)
		where T : class, IAggregate, new() => eventStore.Enlist(correlationId: null, aggregates);

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> with the given <paramref name="correlationId"/>
	/// and enlists all <paramref name="aggregates"/> against this event store.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> responsible for persisting each aggregate.</param>
	/// <param name="correlationId">
	/// Optional correlation ID to bind all events together. When <see langword="null"/>, a new GUID is generated.
	/// </param>
	/// <param name="aggregates">The aggregates to include in the transaction.</param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	public static IEventStoreTransaction Enlist<T>(
		[NotNull] this IEventStore eventStore,
		string? correlationId,
		params T[] aggregates
	)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentNullException.ThrowIfNull(aggregates);

		EventStoreTransaction transaction = new(correlationId);
		foreach (var aggregate in aggregates)
			transaction.Enlist(aggregate, eventStore);

		return transaction;
	}

	/// <summary>
	/// Creates a new <see cref="IEventStoreTransaction"/> and enlists all <paramref name="aggregates"/>
	/// against this event store, applying a shared <paramref name="operationContext"/> to each.
	/// </summary>
	/// <param name="eventStore">The <see cref="IEventStore"/> responsible for persisting each aggregate.</param>
	/// <param name="operationContext">
	/// Optional <see cref="EventStoreOperationContext"/> applied to every aggregate save.
	/// When <see langword="null"/>, the default context is used.
	/// </param>
	/// <param name="aggregates">The aggregates to include in the transaction.</param>
	/// <returns>A new <see cref="IEventStoreTransaction"/> ready to be committed.</returns>
	public static IEventStoreTransaction Enlist<T>(
		[NotNull] this IEventStore eventStore,
		EventStoreOperationContext? operationContext,
		params T[] aggregates
	)
		where T : class, IAggregate, new()
	{
		ArgumentNullException.ThrowIfNull(eventStore);
		ArgumentNullException.ThrowIfNull(aggregates);

		EventStoreTransaction transaction = new(operationContext?.CorrelationId);
		foreach (var aggregate in aggregates)
			transaction.Enlist(aggregate, eventStore, operationContext);

		return transaction;
	}

	#endregion Enlist

	#region SaveAsync

	/// <summary>
	/// Saves the aggregate using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregate">The aggregate to save.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>A <see cref="SaveResult{T}"/> describing the result of the save.</returns>
	public static Task<SaveResult<T>> SaveAsync<T>(
		[NotNull] this IEventStore eventStore,
		T aggregate,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.SaveAsync(aggregate, null, cancellationToken);

	#endregion SaveAsync

	#region DeleteAsync

	/// <summary>
	/// Deletes the aggregate using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregate">The aggregate to delete.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>True when the aggregate was successfully deleted, otherwise false.</returns>
	public static Task<bool> DeleteAsync<T>(
		[NotNull] this IEventStore eventStore,
		T aggregate,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.DeleteAsync(aggregate, null, cancellationToken);

	/// <summary>
	/// Deletes the aggregate with the given id using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to delete.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>True when the aggregate was successfully deleted, otherwise false.</returns>
	public static Task<bool> DeleteAsync<T>(
		[NotNull] this IEventStore eventStore,
		string aggregateId,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.DeleteAsync<T>(aggregateId, null, cancellationToken);

	/// <summary>
	/// Deletes the aggregate with the given id using the supplied operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregateId">The id of the aggregate to delete.</param>
	/// <param name="operationContext">The operational context controlling how the aggregate is deleted.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>True when the aggregate was successfully deleted, otherwise false.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="aggregateId"/> is null or whitespace.</exception>
	public static async Task<bool> DeleteAsync<T>(
		[NotNull] this IEventStore eventStore,
		string aggregateId,
		EventStoreOperationContext? operationContext,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new()
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId, nameof(aggregateId));

		var aggregate = await eventStore.GetAsync<T>(aggregateId, operationContext, cancellationToken);
		return aggregate != null && await eventStore.DeleteAsync(aggregate, operationContext, cancellationToken);
	}

	#endregion DeleteAsync

	#region RestoreAsync

	/// <summary>
	/// Restores a previously deleted aggregate using the default operation context.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type.</typeparam>
	/// <param name="eventStore">The <see cref="IEventStore"/> used as the root object.</param>
	/// <param name="aggregate">The aggregate to restore.</param>
	/// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
	/// <returns>True when the aggregate was successfully restored, otherwise false.</returns>
	public static Task<bool> RestoreAsync<T>(
		[NotNull] this IEventStore eventStore,
		T aggregate,
		CancellationToken cancellationToken = default
	)
		where T : class, IAggregate, new() => eventStore.RestoreAsync(aggregate, null, cancellationToken);

	#endregion RestoreAsync
}
