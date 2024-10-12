using System.Collections.Concurrent;
using MongoDB.Bson.Serialization;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB.StorageClients;

sealed class MongoDBAggregateSerializationProvider : IBsonSerializationProvider
{
	static readonly Type _aggregateInterfaceType = typeof(IAggregate);
	static readonly ConcurrentDictionary<Type, IBsonSerializer> _serializers = [];

	public IBsonSerializer GetSerializer(Type type)
	{
		return _aggregateInterfaceType.IsAssignableFrom(type)
			? GetOrCreateSerializer(type)
			: null!;
	}

	static IBsonSerializer GetOrCreateSerializer(Type type)
	{
		return _serializers.GetOrAdd(type, t =>
		{
			var serializerType = typeof(MongoDBAggregateSerializer<>).MakeGenericType(t);
			return (IBsonSerializer)Activator.CreateInstance(serializerType)!;
		});
	}
}
