using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.AzureStorage;

public interface ITableEventStoreStorageNameBuilder
{
	/// <summary>
	/// Generates the container name used to store aggregate snapshots in.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	string? GetBlobContainerName<T>();

	/// <summary>
	/// Generate the name of table to use for storing the events and other data associated with the <see cref="ITableEventStore{T}"/>.
	/// </summary>
	/// <typeparam name="T">The <see cref="IAggregate"/> type to generate the name for.</typeparam>
	/// <returns>An azure storage table name.</returns>
	/// <remarks>
	/// Examples would be to use the pre-existing configuration to generate a name unique to the process and aggregate.
	/// 
	/// For example, if this was a microservice (the process) was Sample.API and the <typeparamref name="T"/> was DemoAggregate, then a
	/// comment sense approach would be to generate the table name one of the following:
	/// * ESSampleAPIDemoAggregate
	/// * SampleAPIEventStoreDemoAggregate
	/// * SampleAPIDemoAggregateEventStore
	/// 
	/// Each option has positives and negatives, usually regarding their logical grouping.
	/// </remarks>
	string? GetTableName<T>();
}
