using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.SqlServer;

// SQL strings are built from validated identifiers at construction time, not from user input.
#pragma warning disable CA2100

public sealed partial class SqlServerEventStore<T> : ISqlServerEventStore<T>, IDisposable
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
		IAggregateIdFactory? aggregateIdFactory = null)
	{
		_eventNameMapper = eventNameMapper;
		_eventStoreOptions = sqlServerOptions;
		_validator = validator;
		_aggregateIdFactory = aggregateIdFactory;
		_distributedCache = distributedCache;
		_eventStoreTelemetry = eventStoreTelemetry;
		_aggregateChangeNotifier = aggregateChangeNotifier;
		_aggregateRequirementsManager = aggregateRequirementsManager;

		_aggregateTypeShortName = typeof(T).Name;
		_aggregateTypeFullName = typeof(T).FullName ?? _aggregateTypeShortName;

		var aggregateName = _eventNameMapper.InitializeAggregate<T>();
		if (!aggregateName.Contains('.', StringComparison.InvariantCulture))
			_aggregateTypeShortName = aggregateName;

		_client = new SqlServerEventStoreClient(sqlServerOptions.Value);
	}

	public T FulfilRequirements(T aggregate)
	{
		_aggregateRequirementsManager.Fulfil(aggregate);

		return aggregate;
	}

	async Task UpdateCacheAsync(T aggregate, DistributedCacheEntryOptions? cacheEntryOptions, CancellationToken cancellationToken = default)
	{
		cacheEntryOptions = GetCacheEntryOptions(cacheEntryOptions);

		try
		{
			var cacheKey = CreateCacheKey(aggregate.Id());
			if (aggregate.Details.Locked || (aggregate.Details.IsDeleted && _eventStoreOptions.Value.RemoveDeletedFromCache))
				await _distributedCache.RemoveAsync(cacheKey, cancellationToken);
			else
			{
				if (!_eventStoreOptions.Value.CacheMode.HasFlag(EventStoreCachingOptions.StoreInCache))
					return;

				var data = SerializeSnapshot(aggregate);
				await _distributedCache.SetStringAsync(cacheKey, data, cacheEntryOptions, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			_eventStoreTelemetry.CacheUpdateFailure(aggregate.Id(), _aggregateTypeFullName, ex);
		}
	}

	DistributedCacheEntryOptions GetCacheEntryOptions(DistributedCacheEntryOptions? cacheEntryOptions)
		=> cacheEntryOptions ?? new()
		{
			SlidingExpiration = _eventStoreOptions.Value.DefaultCacheSlidingDuration,
		};

	public async IAsyncEnumerable<string> GetAggregateIdsAsync(bool includeDeleted, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var streamVersionId = CreateStreamVersionId(string.Empty);
		// We query all stream versions from the client by aggregate id pattern.
		// Since SQL doesn't easily support this as a single query like MongoDB,
		// we get stream version rows by entity type and aggregate type.
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = new Microsoft.Data.SqlClient.SqlConnection(_eventStoreOptions.Value.ConnectionString);
		await connection.OpenAsync(cancellationToken);

		var quotedSchema = QuoteIdentifierForQuery(_eventStoreOptions.Value.SchemaName);
		var quotedTable = QuoteIdentifierForQuery(_eventStoreOptions.Value.TableName);

		var sql = includeDeleted
			? $"SELECT [AggregateId] FROM {quotedSchema}.{quotedTable} WHERE [AggregateType] = @AggregateType AND [EntityType] = @EntityType"
			: $"SELECT [AggregateId] FROM {quotedSchema}.{quotedTable} WHERE [AggregateType] = @AggregateType AND [EntityType] = @EntityType AND [IsDeleted] = 0";

		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@AggregateType", System.Data.SqlDbType.NVarChar, 450) { Value = _aggregateTypeShortName });
		command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@EntityType", System.Data.SqlDbType.Int) { Value = StreamVersionType });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
			yield return reader.GetString(0);
	}

	async Task<StreamVersionData?> GetStreamVersionAsync(string aggregateId, bool expectedToExist, CancellationToken cancellationToken)
	{
		_eventStoreTelemetry.GetStreamVersionStart(aggregateId);

		var elapsedMilliseconds = 0L;
		StreamVersionData? result = null;
		try
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			var row = await _client.GetByIdAsync(CreateStreamVersionId(aggregateId), cancellationToken);
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
					IsDeleted = row.IsDeleted
				};
				_eventStoreTelemetry.StreamVersionFound(aggregateId, result.Version, result.AggregateType, result.IsDeleted);
			}
		}
		catch (Exception ex)
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

	string CreateStreamVersionId(string aggregateId)
		=> $"s_{_aggregateTypeShortName}_{aggregateId}";

	string CreateEventId(string aggregateId, int version)
		=> $"e_{_aggregateTypeShortName}_{aggregateId}_{$"{version}".PadLeft(_eventStoreOptions.Value.EventSuffixLength, '0')}";

	string CreateIdempotencyCheckId(string aggregateId, string idempotencyId)
		=> $"i_{_aggregateTypeShortName}_{aggregateId}_{idempotencyId}";

	string CreateSnapshotId(string aggregateId)
		=> $"snap_{_aggregateTypeShortName}_{aggregateId}";

	public string CreateCacheKey(string aggregateId)
		=> $"{_aggregateTypeShortName}:{aggregateId}".ToLowerSafe();

	async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
	{
		if (_eventStoreOptions.Value.AutoCreateTable)
			await _client.EnsureTableExistsAsync(cancellationToken);
	}

	static string QuoteIdentifierForQuery(string identifier)
		=> $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

	public void Dispose()
	{
		GC.SuppressFinalize(this);

		_client?.Dispose();
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
