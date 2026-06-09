using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.MongoDB.Snapshots;

public sealed class MongoDBSnapshotEventStoreOptions
{
	public const string MongoDBEventStore = "EventStore:MongoDBSnapshot";

	[Required]
	public string ConnectionString { get; set; } = default!;

	public string? ApplicationName { get; set; }

	[Required]
	public string Database { get; set; } = default!;

	public string? Collection { get; set; }
}
