using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.SqlServer.Snapshot;

public sealed class SqlServerEventStoreOptions
{
	public const string SqlServerEventStore = "EventStore:SqlServerSnapshot";

	[Required]
	public string ConnectionString { get; set; } = default!;

	public string TableName { get; set; } = "Snapshots";

	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// When true, the table will be created if it doesn't exist.
	/// </summary>
	public bool AutoCreateTable { get; set; } = true;
}
