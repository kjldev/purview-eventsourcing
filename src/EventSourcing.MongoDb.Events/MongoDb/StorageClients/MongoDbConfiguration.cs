namespace Purview.EventSourcing.MongoDB.StorageClients;

sealed class MongoDBConfiguration
{
	public string ConnectionString { get; set; } = default!;

	public string? ApplicationName { get; set; }

	public string Database { get; set; } = default!;

	public string Collection { get; set; } = default!;

	public string? ReplicaName { get; set; }
}
