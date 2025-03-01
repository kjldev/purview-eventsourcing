using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.MongoDB.StorageClients;

namespace Purview.EventSourcing.MongoDB.Snapshot;

public partial class MongoDBSnapshotEventStore<T> : IMongoDBSnapshotEventStore<T>
	where T : AggregateBase, new()
{
	readonly IEventStore<T> _eventStore;
	readonly MongoDBClient _mongoDbClient;
	readonly IOptions<MongoDBEventStoreOptions> _mongoDbOptions;
	readonly IMongoDBSnapshotEventStoreTelemetry _telemetry;

	readonly string _aggregateName;

	public MongoDBSnapshotEventStore(
		Internal.INonQueryableEventStore<T> eventStore,
		IOptions<MongoDBEventStoreOptions> mongoDbOptions,
		IMongoDBSnapshotEventStoreTelemetry telemetry,
		IMongoDBClientTelemetry mongoDBClientTelemetry)
	{
		_eventStore = eventStore;
		_mongoDbOptions = mongoDbOptions;
		_telemetry = telemetry;

		_aggregateName = TypeNameHelper.GetName(typeof(T), "Aggregate");
		var collectionName = _mongoDbOptions.Value.Collection ?? $"snapshot-{_aggregateName}-store";
		_mongoDbClient = new(mongoDBClientTelemetry, new()
		{
			ConnectionString = _mongoDbOptions.Value.ConnectionString,
			Database = _mongoDbOptions.Value.Database,
			Collection = collectionName,
			ApplicationName = _mongoDbOptions.Value.ApplicationName
		});
	}

	public async Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		if (await _mongoDbClient.UpsertAsync(aggregate, BuildPredicate(aggregate), cancellationToken))
			_telemetry.SnapshotCreated(_aggregateName);
	}

	static FilterDefinition<T> BuildPredicate(T aggregate)
	{
		var predicate = new FilterDefinitionBuilder<T>()
			.Eq(MongoDBAggregateSerializer<T>.BsonDocuemntIdPropertyName, aggregate.Id());

		return predicate;
	}
}
