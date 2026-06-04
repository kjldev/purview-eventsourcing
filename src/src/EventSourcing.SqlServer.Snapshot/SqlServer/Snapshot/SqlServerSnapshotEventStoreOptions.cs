using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.SqlServer.Snapshot;

/// <summary>
/// Defines a schema and/or table name override for a specific aggregate type in the snapshot store.
/// </summary>
/// <remarks>
/// The key in <see cref="SqlServerSnapshotEventStoreOptions.AggregateTableOverrides"/> should match
/// the aggregate's <c>AggregateType</c> value (i.e., the class name with any trailing
/// "Aggregate" suffix removed, e.g. <c>"Order"</c> for <c>OrderAggregate</c>).
/// </remarks>
public sealed class SqlServerSnapshotAggregateTableOverride
{
	/// <summary>
	/// Overrides the schema name for this aggregate type.
	/// When null, the global <see cref="SqlServerSnapshotEventStoreOptions.SchemaName"/> is used.
	/// </summary>
	public string? SchemaName { get; set; }

	/// <summary>
	/// Overrides the table name for this aggregate type.
	/// When null, the global <see cref="SqlServerSnapshotEventStoreOptions.TableName"/> is used.
	/// </summary>
	public string? TableName { get; set; }
}

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

	/// <summary>
	/// Per-aggregate-type schema and/or table name overrides.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The key is the aggregate's <c>AggregateType</c> short name — typically the class name
	/// with any trailing <c>"Aggregate"</c> suffix removed (e.g. <c>"Order"</c> for
	/// <c>OrderAggregate</c>).
	/// </para>
	/// <para>
	/// Keys are matched case-insensitively. Null override values fall back to the
	/// global <see cref="SchemaName"/> / <see cref="TableName"/>.
	/// </para>
	/// <example>
	/// <code>
	/// // appsettings.json
	/// "EventStore:SqlServerSnapshot": {
	///   "ConnectionString": "...",
	///   "AggregateTableOverrides": {
	///     "Order": { "SchemaName": "orders", "TableName": "Snapshots" },
	///     "Inventory": { "SchemaName": "inventory" }
	///   }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public Dictionary<string, SqlServerSnapshotAggregateTableOverride> AggregateTableOverrides { get; init; } =
	[with(StringComparer.OrdinalIgnoreCase)];
}
