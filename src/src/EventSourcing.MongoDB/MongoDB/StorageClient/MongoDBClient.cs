using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Purview.EventSourcing.MongoDB.StorageClient;

sealed partial class MongoDBClient : IDisposable
{
	readonly IMongoDBClientTelemetry _telemetry;

	readonly MongoDBConfiguration _configuration;
	readonly MongoClient _client;
	readonly IMongoDatabase _database;

	readonly string _collectionName;

	static readonly Lazy<bool> Initialized = new(Init);

#if EventsBasedStore
	readonly static Lazy<bool> EventsInitialized = new(EventsInit);
#endif

	static bool Init()
	{
		try
		{
			BsonSerializer.RegisterSerializationProvider(new MongoDBAggregateSerializationProvider());

			var iEntityType = typeof(IEntity);

			BsonSerializer.RegisterSerializer(new ObjectSerializer(iEntityType.IsAssignableFrom));
		}
#pragma warning disable CA1031
		catch
#pragma warning restore CA1031
		{
			return false;
		}

		return true;
	}

#if EventsBasedStore

	static bool EventsInit()
	{
		try
		{
			Type[] entityType =
			[
				typeof(EventEntity),
				typeof(IdempotencyMarkerEntity),
				typeof(SnapshotEntity),
				typeof(StreamVersionEntity),
			];

			BsonSerializer.RegisterSerializer(new ObjectSerializer(t => Array.IndexOf(entityType, t) > -1));
		}
#pragma warning disable CA1031
		catch
#pragma warning restore CA1031
		{
			return false;
		}

		return true;
	}
#endif

	public MongoDBClient(
		IMongoDBClientTelemetry telemetry,
		MongoDBConfiguration configuration,
		string? databaseOverride = null,
		string? collectionOverride = null
	)
	{
		_telemetry = telemetry;
		_configuration = configuration;

		var settings = MongoClientSettings.FromConnectionString(configuration.ConnectionString);
		settings.ApplicationName = configuration.ApplicationName;

		_client = new MongoClient(settings);
		_database = _client.GetDatabase(databaseOverride ?? configuration.Database);
		_collectionName = collectionOverride ?? configuration.Collection;

		if (Initialized.Value)
			_telemetry.Initialized();

#if EventsBasedStore

		if (EventsInitialized.Value)
			_telemetry.EventsInitialized();
#endif
	}

	static FilterDefinition<T> BuildPredicate<T>(string id, int? entityType)
	{
		var builder = new FilterDefinitionBuilder<T>();

		return entityType == null
			? builder.Eq("_id", id)
			: builder.And(builder.Eq("_id", id), builder.Eq(nameof(IEntity.EntityType), entityType));
	}

	sealed class StringObjectIdIdGeneratorConventionThatWorks : ConventionBase, IPostProcessingConvention
	{
		public void PostProcess(BsonClassMap classMap)
		{
			var idMemberMap = classMap.IdMemberMap;
			if (idMemberMap == null || idMemberMap.IdGenerator != null)
				return;

			if (idMemberMap.MemberType == typeof(string))
			{
				idMemberMap
					.SetIdGenerator(StringObjectIdGenerator.Instance)
					.SetSerializer(new StringSerializer(BsonType.ObjectId));
			}
		}
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);

		_client?.Dispose();
	}
}
