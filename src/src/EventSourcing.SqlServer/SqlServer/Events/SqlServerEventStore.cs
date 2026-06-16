using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Events.Upcasting;
using Purview.EventSourcing.Aggregates.Snapshotting;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.SqlServer.Events;

// SQL strings are built from validated identifiers at construction time, not from user input.
#pragma warning disable CA2100

public sealed partial class SqlServerEventStore<T> : ISqlServerEventStore<T>, ITransactionalEventStore<T>
	where T : class, IAggregate, new()
{
	const int StreamVersionType = 0;
	const int EventType = 1;
	const int IdempotencyMarkerType = 2;
	const int SnapshotType = 3;

	readonly SqlServerEventStoreClient _client;

	readonly IAggregateEventNameMapper _eventNameMapper;
	readonly IOptions<SqlServerEventStoreOptions> _eventStoreOptions;
	readonly FluentValidation.IValidator<T>? _validator;
	readonly IAggregateIdFactory? _aggregateIdFactory;
	readonly IDistributedCache _distributedCache;
	readonly ISqlServerEventStoreTelemetry _eventStoreTelemetry;
	readonly ChangeFeed.IAggregateChangeFeedNotifier<T> _aggregateChangeNotifier;
	readonly IAggregateRequirementsManager _aggregateRequirementsManager;
	readonly IEventUpcasterRegistry? _eventUpcasterRegistry;
	readonly ISnapshotStrategy<T> _snapshotStrategy;

	readonly string _aggregateTypeFullName;
	readonly string _aggregateTypeShortName;

	public SqlServerEventStore(
		IAggregateEventNameMapper eventNameMapper,
		[NotNull] IOptions<SqlServerEventStoreOptions> sqlServerOptions,
		IDistributedCache distributedCache,
		ISqlServerEventStoreTelemetry eventStoreTelemetry,
		ChangeFeed.IAggregateChangeFeedNotifier<T> aggregateChangeNotifier,
		IAggregateRequirementsManager aggregateRequirementsManager,
		FluentValidation.IValidator<T>? validator = null,
		IAggregateIdFactory? aggregateIdFactory = null,
		IEventUpcasterRegistry? eventUpcasterRegistry = null,
		ISnapshotStrategy<T>? snapshotStrategy = null
	)
	{
		_eventNameMapper = eventNameMapper;
		_eventStoreOptions = sqlServerOptions;
		_validator = validator;
		_aggregateIdFactory = aggregateIdFactory;
		_distributedCache = distributedCache;
		_eventStoreTelemetry = eventStoreTelemetry;
		_aggregateChangeNotifier = aggregateChangeNotifier;
		_aggregateRequirementsManager = aggregateRequirementsManager;
		_eventUpcasterRegistry = eventUpcasterRegistry;
		_snapshotStrategy =
			snapshotStrategy ?? new IntervalSnapshotStrategy<T>(sqlServerOptions.Value.SnapshotInterval);

		_aggregateTypeShortName = typeof(T).Name;
		_aggregateTypeFullName = typeof(T).FullName ?? _aggregateTypeShortName;

		var aggregateName = _eventNameMapper.InitializeAggregate<T>();
		if (!aggregateName.Contains('.', StringComparison.InvariantCulture))
			_aggregateTypeShortName = aggregateName;

		var clientOptions = ResolveClientOptions(sqlServerOptions.Value, _aggregateTypeShortName);
		_client = new SqlServerEventStoreClient(clientOptions);
	}

	public T FulfilRequirements(T aggregate)
	{
		_aggregateRequirementsManager.Fulfil(aggregate);

		return aggregate;
	}

	async Task UpdateCacheAsync(
		T aggregate,
		DistributedCacheEntryOptions? cacheEntryOptions,
		CancellationToken cancellationToken = default
	)
	{
		cacheEntryOptions = GetCacheEntryOptions(cacheEntryOptions);

		try
		{
			var cacheKey = CreateCacheKey(aggregate.Id());
			if (
				aggregate.Details.Locked
				|| (aggregate.Details.IsDeleted && _eventStoreOptions.Value.RemoveDeletedFromCache)
			)
				await _distributedCache.RemoveAsync(cacheKey, cancellationToken);
			else
			{
				if (!_eventStoreOptions.Value.CacheMode.HasFlag(EventStoreCachingOptions.StoreInCache))
					return;

				var data = SerializeSnapshot(aggregate);
				await _distributedCache.SetStringAsync(cacheKey, data, cacheEntryOptions, cancellationToken);
			}
		}
#pragma warning disable CA1031
		catch (Exception ex)
#pragma warning restore CA1031
		{
			_eventStoreTelemetry.CacheUpdateFailure(aggregate.Id(), _aggregateTypeFullName, ex);
		}
	}

	DistributedCacheEntryOptions GetCacheEntryOptions(DistributedCacheEntryOptions? cacheEntryOptions) =>
		cacheEntryOptions ?? new() { SlidingExpiration = _eventStoreOptions.Value.DefaultCacheSlidingDuration };

	public async IAsyncEnumerable<string> GetAggregateIdsAsync(
		bool includeDeleted,
		[EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await foreach (
			var aggregateId in _client.GetAggregateIdsByTypeAsync(
				_aggregateTypeShortName,
				includeDeleted,
				cancellationToken
			)
		)
			yield return aggregateId;
	}

	Task<StreamVersionData?> GetStreamVersionAsync(
		string aggregateId,
		bool expectedToExist,
		CancellationToken cancellationToken
	) => GetStreamVersionAsync(aggregateId, expectedToExist, null, null, cancellationToken);

	async Task<StreamVersionData?> GetStreamVersionAsync(
		string aggregateId,
		bool expectedToExist,
		Microsoft.Data.SqlClient.SqlConnection? connection,
		Microsoft.Data.SqlClient.SqlTransaction? transaction,
		CancellationToken cancellationToken
	)
	{
		_eventStoreTelemetry.GetStreamVersionStart(aggregateId);

		var elapsedMilliseconds = 0L;
		StreamVersionData? result = null;
		try
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			var row = connection is null
				? await _client.GetByIdAsync(CreateStreamVersionId(aggregateId), cancellationToken)
				: await _client.GetByIdAsync(
					CreateStreamVersionId(aggregateId),
					connection,
					transaction,
					cancellationToken
				);
			sw.Stop();

			elapsedMilliseconds = sw.ElapsedMilliseconds;

			if (row == null || row.EntityType != StreamVersionType)
			{
				if (expectedToExist)
					_eventStoreTelemetry.StreamVersionExpectedToExistButNotFound(aggregateId);
				else
					_eventStoreTelemetry.StreamVersionNotFound(aggregateId);
			}
			else
			{
				result = new StreamVersionData
				{
					Id = row.Id,
					AggregateId = row.AggregateId,
					AggregateType = row.AggregateType,
					Version = row.Version,
					IsDeleted = row.IsDeleted,
				};
				_eventStoreTelemetry.StreamVersionFound(
					aggregateId,
					result.Version,
					result.AggregateType,
					result.IsDeleted
				);
			}
		}
#pragma warning disable CA1031
		catch (Exception ex)
#pragma warning restore CA1031
		{
			_eventStoreTelemetry.GetStreamVersionFailed(aggregateId, ex);
		}

		_eventStoreTelemetry.GetStreamVersionComplete(aggregateId, elapsedMilliseconds);

		return result;
	}

	static bool ReturnAggregate(bool isDeleted, string aggregateId, EventStoreOperationContext context)
	{
		if (isDeleted)
		{
			switch (context.DeleteMode)
			{
				case DeleteHandlingMode.ThrowsException:
					throw AggregateIsDeletedException(aggregateId);
				case DeleteHandlingMode.ReturnsNull:
					return false;
			}
		}

		return true;
	}

	string CreateStreamVersionId(string aggregateId) => $"s_{_aggregateTypeShortName}_{aggregateId}";

	string CreateEventId(string aggregateId, int version) =>
		$"e_{_aggregateTypeShortName}_{aggregateId}_{$"{version}".PadLeft(_eventStoreOptions.Value.EventSuffixLength, '0')}";

	string CreateIdempotencyCheckId(string aggregateId, string idempotencyId) =>
		$"i_{_aggregateTypeShortName}_{aggregateId}_{idempotencyId}";

	string CreateSnapshotId(string aggregateId) => $"snap_{_aggregateTypeShortName}_{aggregateId}";

	public string CreateCacheKey(string aggregateId) => $"{_aggregateTypeShortName}:{aggregateId}".ToUpperInvariant();

	async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
	{
		if (_eventStoreOptions.Value.AutoCreateTable)
			await _client.EnsureTableExistsAsync(cancellationToken);
	}

	/// <summary>
	/// Merges global options with any per-aggregate-type table override.
	/// Returns an options instance with the effective schema and table name.
	/// </summary>
	static SqlServerEventStoreOptions ResolveClientOptions(SqlServerEventStoreOptions options, string aggregateTypeName)
	{
		return !options.AggregateTableOverrides.TryGetValue(aggregateTypeName, out var ovr) || ovr is null ? options
			: ovr.SchemaName is null && ovr.TableName is null ? options
			: new SqlServerEventStoreOptions
			{
				ConnectionString = options.ConnectionString,
				SchemaName = ovr.SchemaName ?? options.SchemaName,
				TableName = ovr.TableName ?? options.TableName,
				AutoCreateTable = options.AutoCreateTable,
				UseDataCompression = options.UseDataCompression,
				TimeoutInSeconds = options.TimeoutInSeconds,
				MaxEventCountOnSave = options.MaxEventCountOnSave,
				SnapshotInterval = options.SnapshotInterval,
				RemoveDeletedFromCache = options.RemoveDeletedFromCache,
				EventSuffixLength = options.EventSuffixLength,
				CacheMode = options.CacheMode,
				DefaultCacheSlidingDuration = options.DefaultCacheSlidingDuration,
				RequiresValidPrincipalIdentifier = options.RequiresValidPrincipalIdentifier,
			};
	}

	internal sealed class StreamVersionData
	{
		public string Id { get; set; } = default!;
		public string AggregateId { get; set; } = default!;
		public string AggregateType { get; set; } = default!;
		public int Version { get; set; }
		public bool IsDeleted { get; set; }
	}
}
