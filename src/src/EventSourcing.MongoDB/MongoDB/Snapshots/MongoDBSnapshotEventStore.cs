using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Snapshotting;
using Purview.EventSourcing.MongoDB.StorageClient;

namespace Purview.EventSourcing.MongoDB.Snapshots;

public sealed partial class MongoDBSnapshotEventStore<T> : IMongoDBSnapshotEventStore<T>, IDisposable
	where T : AggregateBase, new()
{
	readonly IEventStoreCore<T> _eventStore;
	readonly MongoDBClient _mongoDbClient;
	readonly IOptions<MongoDBSnapshotEventStoreOptions> _mongoDbOptions;
	readonly IMongoDBSnapshotEventStoreTelemetry _telemetry;
	readonly ISnapshotStrategy<T> _snapshotStrategy;
	readonly ISnapshotStrategySelector? _snapshotStrategySelector;

	readonly string _aggregateName;

	public MongoDBSnapshotEventStore(
		Internal.INonQueryableEventStore<T> eventStore,
		IOptions<MongoDBSnapshotEventStoreOptions> mongoDbOptions,
		IMongoDBSnapshotEventStoreTelemetry telemetry,
		IMongoDBClientTelemetry mongoDBClientTelemetry,
		ISnapshotStrategy<T>? snapshotStrategy = null,
		ISnapshotStrategySelector? snapshotStrategySelector = null
	)
	{
		_eventStore = eventStore;
		_mongoDbOptions = mongoDbOptions;
		_telemetry = telemetry;
		_snapshotStrategy = snapshotStrategy ?? new AlwaysSnapshotStrategy<T>();
		_snapshotStrategySelector = snapshotStrategySelector;

		_aggregateName = TypeNameHelper.GetName(typeof(T), "Aggregate");
		var collectionName = _mongoDbOptions.Value.Collection ?? $"snapshot-{_aggregateName}-store";
		_mongoDbClient = new(
			mongoDBClientTelemetry,
			new()
			{
				ConnectionString = _mongoDbOptions.Value.ConnectionString,
				Database = _mongoDbOptions.Value.Database,
				Collection = collectionName,
				ApplicationName = _mongoDbOptions.Value.ApplicationName,
			}
		);
	}

	public async Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		if (await _mongoDbClient.UpsertAsync(aggregate, BuildPredicate(aggregate), cancellationToken))
			_telemetry.SnapshotCreated(_aggregateName);
	}

	static FilterDefinition<T> BuildPredicate(T aggregate)
	{
		var predicate = new FilterDefinitionBuilder<T>().Eq(
			MongoDBAggregateSerializer<T>.BsonDocuemntIdPropertyName,
			aggregate.Id()
		);

		return predicate;
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		_mongoDbClient?.Dispose();
	}
}
