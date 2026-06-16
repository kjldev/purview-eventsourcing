using System.Collections.Concurrent;
using MongoDB.Bson.Serialization;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB.StorageClient;

sealed class MongoDBAggregateSerializationProvider : IBsonSerializationProvider
{
	static readonly Type AggregateInterfaceType = typeof(IAggregate);
	static readonly ConcurrentDictionary<Type, IBsonSerializer> Serializers = [];

	public IBsonSerializer GetSerializer(Type type)
	{
		return AggregateInterfaceType.IsAssignableFrom(type) ? GetOrCreateSerializer(type) : null!;
	}

	static IBsonSerializer GetOrCreateSerializer(Type type)
	{
		return Serializers.GetOrAdd(
			type,
			t =>
			{
				var serializerType = typeof(MongoDBAggregateSerializer<>).MakeGenericType(t);
				return (IBsonSerializer)Activator.CreateInstance(serializerType)!;
			}
		);
	}
}
