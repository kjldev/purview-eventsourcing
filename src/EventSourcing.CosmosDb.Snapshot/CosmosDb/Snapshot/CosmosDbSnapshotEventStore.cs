using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Internal;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

public sealed partial class CosmosDbSnapshotEventStore<T> : ICosmosDbSnapshotEventStore<T>
	where T : class, IAggregate, new()
{
	readonly IEventStore<T> _eventStore;
	readonly IOptions<CosmosDbEventStoreOptions> _cosmosDbEventStoreOptions;
	readonly ICosmosDbSnapshotEventStoreTelemetry _telemetry;

	readonly CosmosDbClient _cosmosDbClient;

	readonly PartitionKey _partitionKey;

	readonly Type _aggregateType = typeof(T);
	readonly string _aggregateName;

	static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, string> AggregateTypeNames = new();

	public CosmosDbSnapshotEventStore(
		// Explicitly request a non-queryable event store.
		INonQueryableEventStore<T> eventStore,
		IOptions<CosmosDbEventStoreOptions> cosmosDbEventStoreOptions,
		ICosmosDbSnapshotEventStoreTelemetry telemetry,
		CosmosClient? cosmosClient = null)
	{
		_eventStore = eventStore;
		_cosmosDbEventStoreOptions = cosmosDbEventStoreOptions;
		_telemetry = telemetry;

		_partitionKey = new(GetAggregateTypeName());

		_cosmosDbClient = new CosmosDbClient(_cosmosDbEventStoreOptions.Value, cosmosClient: cosmosClient);
		_aggregateName = TypeNameHelper.GetName(_aggregateType, "Aggregate");
	}

	/// <summary>
	/// This will upsert the aggregate regardless of it's save state in the internal event store.
	/// </summary>
	/// <param name="aggregate"></param>
	/// <param name="cancellationToken"></param>
	public async Task SnapshotAsync(T aggregate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(aggregate, nameof(aggregate));

		var result = await _cosmosDbClient.UpsertAsync(aggregate, _partitionKey, cancellationToken);
		if (result.IsSuccessStatusCode)
			_telemetry.SnapshotCreated(_aggregateName);
	}

	string GetAggregateTypeName()
		=> AggregateTypeNames.GetOrAdd(_aggregateType, _ => new T().AggregateType);
}
