using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.SqlServer.Snapshot;

public sealed class SqlServerSnapshotEventStoreOptions
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

	/// <summary>
	/// <para>When true, PAGE data compression is applied to the table and indices during auto-creation.</para>
	/// <para>This significantly reduces I/O for NVARCHAR(MAX) payload columns and improves read/write throughput.</para>
	/// <para>Available on SQL Server Enterprise Edition and all Azure SQL tiers.</para>
	/// </summary>
	/// <remarks>Only applies when <see cref="AutoCreateTable"/> is true and the table is being created for the first time.</remarks>
	public bool UseDataCompression { get; set; } = true;
}
