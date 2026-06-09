using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB.StorageClient;

sealed class MongoDBAggregateSerializer<TAggregate> : SerializerBase<TAggregate>, IBsonDocumentSerializer
	where TAggregate : class, IAggregate, new()
{
	public const string BsonDocuemntIdPropertyName = "_id";

	public override TAggregate Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
	{
		var serializer = BsonSerializer.LookupSerializer(typeof(BsonDocument));
		var document = serializer.Deserialize(context, args);

		var bsonDocument = document.ToBsonDocument();
		var result = bsonDocument.ToJson();

		return EventStoreSerializationHelpers.Deserialize<TAggregate>(result)!;
	}

	public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TAggregate value)
	{
		var jsonDocument = EventStoreSerializationHelpers.Serialize(value, value.GetType());
		var bsonDocument = BsonSerializer.Deserialize<BsonDocument>(jsonDocument);
		bsonDocument.Add(BsonDocuemntIdPropertyName, value.Details.Id);

		var serializer = BsonSerializer.LookupSerializer(typeof(BsonDocument));
		var doc = bsonDocument.AsBsonValue;

		serializer.Serialize(context, doc);
	}

	public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo? serializationInfo)
	{
		var memberType = ValueType.GetProperty(memberName)?.PropertyType;
		if (memberType == null)
		{
			serializationInfo = null;
			return false;
		}

		serializationInfo = new(memberName, BsonSerializer.LookupSerializer(memberType), ValueType);

		return true;
	}
}
