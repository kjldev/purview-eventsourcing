using Microsoft.Extensions.Caching.Distributed;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.AzureStorage;
using Purview.EventSourcing.AzureStorage.StorageClients.Blob;
using Purview.EventSourcing.AzureStorage.StorageClients.Table;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.Services;
using Purview.EventSourcing.SqlServer;
using Purview.EventSourcing.SqlServer.Snapshot;
using Purview.EventSourcing.SqlServer.Snapshots;

namespace Purview.EventSourcing.Fixtures.SqlServer;

public sealed class SqlServerSnapshotTestContext
{
	readonly string _sqlServerConnectionString;
	readonly string _azuriteConnectionString;

	ITableEventStoreTelemetry _telemetry = default!;
	IAggregateEventNameMapper _eventNameMapper = default!;

	public Guid RunId { get; } = Guid.NewGuid();

	internal SqlServerClient SqlServerClient { get; private set; } = default!;

	internal AzureTableClient TableClient { get; private set; } = default!;

	internal AzureBlobClient BlobClient { get; private set; } = default!;

	public SqlServerSnapshotEventStore<PersistenceAggregate> EventStore { get; init; }

	public SqlServerSnapshotTestContext(
		string sqlServerConnectionString,
		string azuriteConnectionString,
		int correlationIdsToGenerate = 1,
		string? tableName = null
	)
	{
		_sqlServerConnectionString = sqlServerConnectionString;
		_azuriteConnectionString = azuriteConnectionString;

		EventStore = CreateSqlServerEventStore(correlationIdsToGenerate, tableName);
	}

	SqlServerSnapshotEventStore<PersistenceAggregate> CreateSqlServerEventStore(
		int correlationIdsToGenerate = 1,
		string? tableName = null
	)
	{
		var tableEventStore = CreateTableEventStore(correlationIdsToGenerate);

		var resolvedTableName = tableName ?? $"Snapshots_{RunId:N}";

		SqlServerSnapshotEventStoreOptions config = new()
		{
			ConnectionString = _sqlServerConnectionString,
			TableName = resolvedTableName,
			SchemaName = "dbo",
			AutoCreateTable = true,
		};

		SqlServerSnapshotEventStore<PersistenceAggregate> eventStore = new(
			tableEventStore,
			Microsoft.Extensions.Options.Options.Create(config),
			Substitute.For<ISqlServerSnapshotEventStoreTelemetry>()
		);

		SqlServerClient = new(
			new SqlServerClientOptions
			{
				ConnectionString = config.ConnectionString,
				TableName = config.TableName,
				SchemaName = config.SchemaName,
				AutoCreateTable = config.AutoCreateTable,
			}
		);

		return eventStore;
	}

	TableEventStore<PersistenceAggregate> CreateTableEventStore(int correlationIdsToGenerate = 1)
	{
		var runIds = Enumerable
			.Range(1, correlationIdsToGenerate)
			.Select(_ => $"{Guid.NewGuid()}".ToUpperInvariant())
			.ToArray();

		_eventNameMapper = new AggregateEventNameMapper();
		_telemetry = Substitute.For<ITableEventStoreTelemetry>();

		AzureStorageEventStoreOptions azureStorageOptions = new()
		{
			ConnectionString = _azuriteConnectionString,
			Table = TestHelpers.GenAzureTableName(RunId),
			Container = TestHelpers.GenAzureBlobContainerName(RunId),
			TimeoutInSeconds = 10,
			RemoveDeletedFromCache = true,
			SnapshotInterval = 1,
		};

		TableEventStore<PersistenceAggregate> eventStore = new(
			eventNameMapper: _eventNameMapper,
			azureStorageOptions: Microsoft.Extensions.Options.Options.Create(azureStorageOptions),
			distributedCache: Substitute.For<IDistributedCache>(),
			aggregateChangeNotifier: Substitute.For<IAggregateChangeFeedNotifier<PersistenceAggregate>>(),
			eventStoreTelemetry: _telemetry,
			aggregateRequirementsManager: Substitute.For<IAggregateRequirementsManager>()
		);

		TableClient = new(azureStorageOptions, eventStore.TableName);
		BlobClient = new(azureStorageOptions, eventStore.ContainerName);

		return eventStore;
	}
}
