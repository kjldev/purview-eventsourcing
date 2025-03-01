using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.MongoDB;

public interface IMongoDBEventStoreStorageNameBuilder
{
	/// <summary>
	/// Generates the collection name used to store aggregate events, stream version and idempotency markers in.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	string? GetEventsCollectionName<T>();

	/// <summary>
	/// Generates the collection name used to store aggregate snapshots in.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	string? GetSnapshotCollectionName<T>();

	/// <summary>
	/// Generate the name of table to use for storing the events and other data associated with the <see cref="IMongoDBEventStore{T}"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type to generate the name for.</typeparam>
	/// <returns>An azure storage table name.</returns>	
	string? GetDatabaseName<T>();
}
