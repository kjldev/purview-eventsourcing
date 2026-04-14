using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.SqlServer.Snapshot;

public sealed partial class SqlServerSnapshotEventStore<T> : ISqlServerSnapshotEventStore<T>, ITransactionalEventStore<T>, IDisposable
	where T : class, IAggregate, new()
{
	readonly IEventStoreImpl<T> _eventStore;
	readonly IOptions<SqlServerSnapshotEventStoreOptions> _sqlServerEventStoreOptions;
	readonly ISqlServerSnapshotEventStoreTelemetry _telemetry;

	readonly SqlServerClient _sqlServerClient;

	readonly Type _aggregateType = typeof(T);
	readonly string _aggregateName;

	static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string> AggregateTypeNames = new();

	public SqlServerSnapshotEventStore(
		// Explicitly request a non-queryable event store.
		INonQueryableEventStore<T> eventStore,
		IOptions<SqlServerSnapshotEventStoreOptions> sqlServerEventStoreOptions,
		ISqlServerSnapshotEventStoreTelemetry telemetry
	)
	{
		_eventStore = eventStore;
		_sqlServerEventStoreOptions = sqlServerEventStoreOptions;
		_telemetry = telemetry;

		_aggregateName = TypeNameHelper.GetName(_aggregateType, "Aggregate");

		var clientOptions = ResolveClientOptions(_sqlServerEventStoreOptions.Value, _aggregateName);
		_sqlServerClient = new SqlServerClient(clientOptions);
	}

	/// <summary>
	/// Merges global options with any per-aggregate-type table override.
	/// </summary>
	static SqlServerClientOptions ResolveClientOptions(SqlServerSnapshotEventStoreOptions options, string aggregateName)
	{
		var schema = options.SchemaName;
		var table = options.TableName;

		if (options.AggregateTableOverrides.TryGetValue(aggregateName, out var ovr) && ovr is not null)
		{
			schema = ovr.SchemaName ?? schema;
			table = ovr.TableName ?? table;
		}

		return new SqlServerClientOptions
		{
			ConnectionString = options.ConnectionString,
			TableName = table,
			SchemaName = schema,
			AutoCreateTable = options.AutoCreateTable,
			UseDataCompression = options.UseDataCompression,
		};
	}

	/// <summary>
	/// This will upsert the aggregate regardless of it's save state in the internal event store.
	/// </summary>
	/// <param name="aggregate"></param>
	/// <param name="cancellationToken"></param>
	public async Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		_telemetry.SnapshotSaveStart(aggregate.Details.Id, _aggregateName);
		using var activity = _telemetry.SnapshotSave(aggregate.Details.Id, _aggregateName);

		try
		{
			var result = await _sqlServerClient.UpsertAsync(
				aggregate,
				aggregate.Details.Id,
				GetAggregateTypeName(),
				cancellationToken
			);
			if (result)
			{
				_telemetry.SnapshotCreated(_aggregateName);
				_telemetry.SnapshotSaveComplete(aggregate.Details.Id, _aggregateName);
			}
		}
		catch (Exception ex)
		{
			_telemetry.SnapshotSaveFailed(aggregate.Details.Id, _aggregateName, ex);
			throw;
		}
	}

	string GetAggregateTypeName() => AggregateTypeNames.GetOrAdd(_aggregateType, _ => new T().AggregateType);

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		_sqlServerClient?.Dispose();
	}
}
