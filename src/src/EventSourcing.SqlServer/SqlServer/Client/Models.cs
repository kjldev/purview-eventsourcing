namespace Purview.EventSourcing.SqlServer.Client;

sealed record SnapshotStorageRow(string Id, string AggregateType, string Payload);

sealed class SnapshotQueryRow<TAggregate>
	where TAggregate : class
{
	public required string Id { get; set; }

	public required string AggregateType { get; set; }

	public required TAggregate Payload { get; set; }
}

sealed record SqlServerClientOptions(string ConnectionString, bool UseDataCompression)
{
	public string TableName { get; init; } = "Snapshots";

	public string SchemaName { get; init; } = "dbo";

	public bool AutoCreateTable { get; init; } = true;
}
