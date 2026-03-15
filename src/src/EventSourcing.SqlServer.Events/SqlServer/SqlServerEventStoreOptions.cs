using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.SqlServer;

/// <summary>
/// Defines a schema and/or table name override for a specific aggregate type.
/// </summary>
/// <remarks>
/// The key in <see cref="SqlServerEventStoreOptions.AggregateTableOverrides"/> should match
/// the aggregate's <c>AggregateType</c> value (i.e., the class name with any trailing
/// "Aggregate" suffix removed, e.g. <c>"Order"</c> for <c>OrderAggregate</c>).
/// </remarks>
public sealed class SqlServerAggregateTableOverride
{
	/// <summary>
	/// Overrides the schema name for this aggregate type.
	/// When null, the global <see cref="SqlServerEventStoreOptions.SchemaName"/> is used.
	/// </summary>
	public string? SchemaName { get; set; }

	/// <summary>
	/// Overrides the table name for this aggregate type.
	/// When null, the global <see cref="SqlServerEventStoreOptions.TableName"/> is used.
	/// </summary>
	public string? TableName { get; set; }
}

public sealed class SqlServerEventStoreOptions
{
	public const string SqlServerEventStore = "EventStore:SqlServer";

	/// <summary>
	/// Defines the default snapshot interval, when not set in configuration.
	/// Defaults to 1, which means snapshot when at least 1 event is saved.
	/// </summary>
	/// <seealso cref="SnapshotInterval"/>
	public static int DefaultSnapshotInterval { get; set; } = 1;

	const bool DefaultRemoveDeletedFromCache = true;
	const int DefaultEventSuffixLength = 30;

	[Required]
	public string ConnectionString { get; set; } = default!;

	public string TableName { get; set; } = "EventStore";

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

	[Range(1, 120000)]
	public int? TimeoutInSeconds { get; set; } = 60;

	/// <summary>
	/// The maximum number of events to save in a single operation.
	/// </summary>
	[Range(1, 10_000)]
	public int MaxEventCountOnSave { get; set; } = 1000;

	/// <summary>
	/// <para>
	/// Indicates when a snapshot is made of the aggregate, based on the number of events
	/// applied during a <see cref="IEventStore{T}.SaveAsync(T, EventStoreOperationContext?, CancellationToken)"/> operation.
	/// </para>
	/// <para>The default is 1, so a snapshot is made for any change.</para>
	/// </summary>
	/// <remarks>The default can be changed statically by setting the <see cref="DefaultSnapshotInterval"/>.</remarks>
	/// <see cref="IEventStore{T}"/>
	[Range(1, int.MaxValue)]
	public int SnapshotInterval { get; set; } = DefaultSnapshotInterval;

	/// <summary>
	/// <para>Indicates if a deleted aggregate is removed from cache. Defaults to true.</para>
	/// <para>
	/// If true, when an aggregate is deleted, it is removed from the cache.
	/// Or in the case of a get, it is not placed in cache for future calls.
	/// </para>
	/// <para>If false, a deleted aggregate can be placed into cache.</para>
	/// </summary>
	[DefaultValue(DefaultRemoveDeletedFromCache)]
	public bool RemoveDeletedFromCache { get; set; } = DefaultRemoveDeletedFromCache;

	/// <summary>
	/// The length of the suffix when creating event records.
	/// </summary>
	/// <remarks>Changing this where data already exists will result in incomplete aggregates.</remarks>
	[Required]
	[Range(10, 100)]
	[DefaultValue(DefaultEventSuffixLength)]
	public int EventSuffixLength { get; set; } = DefaultEventSuffixLength;

	/// <summary>
	/// Gets/ sets a value indicating how the <see cref="IEventStore{T}"/>
	/// uses the <see cref="IDistributedCache"/> during it's operations. Defaults to <see cref="EventStoreCachingOptions.GetAndStore"/>.
	/// </summary>
	[DefaultValue(EventStoreCachingOptions.GetAndStore)]
	public EventStoreCachingOptions CacheMode { get; set; } = EventStoreCachingOptions.GetAndStore;

	public TimeSpan DefaultCacheSlidingDuration { get; set; } = TimeSpan.FromMinutes(60);

	/// <summary>
	/// <para>
	/// Gets/ sets a value indicating if a valid identifier from a <see cref="ClaimsPrincipal"/> is required when
	/// saving aggregates.
	/// </para>
	/// <para>
	/// Sets the <see cref="EventStoreOperationContext.RequiresValidPrincipalIdentifier"/> to this value
	/// on the <see cref="EventStoreOperationContext.Default"/> property.
	/// </para>
	/// </summary>
	/// <remarks>If true and <see cref="IPrincipalService.Identifier()"/> returns null or empty string, an exception is thrown.</remarks>
	[DefaultValue(true)]
	public bool RequiresValidPrincipalIdentifier { get; set; } = true;

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
	/// "EventStore:SqlServer": {
	///   "ConnectionString": "...",
	///   "AggregateTableOverrides": {
	///     "Order": { "SchemaName": "orders", "TableName": "EventStore" },
	///     "Inventory": { "SchemaName": "inventory" }
	///   }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public Dictionary<string, SqlServerAggregateTableOverride> AggregateTableOverrides { get; init; }
		= new(StringComparer.OrdinalIgnoreCase);
}
