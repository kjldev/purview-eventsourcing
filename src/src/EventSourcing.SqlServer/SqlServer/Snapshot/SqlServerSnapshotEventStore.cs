using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.SqlServer.Snapshot;

public sealed partial class SqlServerSnapshotEventStore<T> : ISqlServerSnapshotEventStore<T>, IDisposable
	where T : class, IAggregate, new()
{
	readonly IEventStore<T> _eventStore;
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
		_sqlServerClient = new SqlServerClient(
			new SqlServerClientOptions
			{
				ConnectionString = _sqlServerEventStoreOptions.Value.ConnectionString,
				TableName = _sqlServerEventStoreOptions.Value.TableName,
				SchemaName = _sqlServerEventStoreOptions.Value.SchemaName,
				AutoCreateTable = _sqlServerEventStoreOptions.Value.AutoCreateTable,
				UseDataCompression = _sqlServerEventStoreOptions.Value.UseDataCompression,
			}
		);
	}

	/// <summary>
	/// This will upsert the aggregate regardless of it's save state in the internal event store.
	/// </summary>
	/// <param name="aggregate"></param>
	/// <param name="cancellationToken"></param>
	public async Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		var result = await _sqlServerClient.UpsertAsync(
			aggregate,
			aggregate.Details.Id,
			GetAggregateTypeName(),
			cancellationToken
		);
		if (result)
			_telemetry.SnapshotCreated(_aggregateName);
	}

	string GetAggregateTypeName() => AggregateTypeNames.GetOrAdd(_aggregateType, _ => new T().AggregateType);

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		_sqlServerClient?.Dispose();
	}
}
