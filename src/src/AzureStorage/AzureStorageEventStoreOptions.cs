using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;

namespace Purview.EventSourcing.AzureStorage;

/// <summary>
/// Options for configuring the Azure Table and Blob Storage event store provider.
/// </summary>
/// <remarks>
/// Bound from the <see cref="AzureStorageEventStore"/> configuration section when registered through
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions"/>. Options are validated on start-up.
/// </remarks>
public sealed class AzureStorageEventStoreOptions
{
	/// <summary>
	/// The configuration section name that the options are bound from.
	/// </summary>
	public const string AzureStorageEventStore = "EventStore:AzureStorage";

	const bool DefaultRemoveDeletedFromCache = true;
	const int DefaultEventSuffixLength = 30;
	const string DefaultEventPrefix = "e";

	/// <summary>
	/// Gets or sets the connection string used to access Azure Table and Blob Storage.
	/// </summary>
	/// <remarks>When unset, the connection string is resolved from the registered configuration.</remarks>
	[Required]
	public string ConnectionString { get; set; } = default!;

	/// <summary>
	/// Gets or sets the name of the Azure Table used to store events and related entities.
	/// </summary>
	[Required]
	[StringLength(63, MinimumLength = 3)]
	[RegularExpression("^[A-Za-z][A-Za-z0-9]*$")]
	public string Table { get; set; } = "EventStore";

	/// <summary>
	/// Container name used to create the blob container for storing snapshots and large events.
	/// </summary>
	[StringLength(63, MinimumLength = 3)]
	[RegularExpression("^[a-z0-9](?!.*--)[a-z0-9-]{1,61}[a-z0-9]$")]
	public string Container { get; set; } = "snapshots";

	/// <summary>
	/// Gets or sets the timeout, in seconds, applied to Azure Storage client operations.
	/// </summary>
	/// <remarks>When <see langword="null"/>, the Azure SDK default network timeout is used.</remarks>
	[Range(1, 120000)]
	public int? TimeoutInSeconds { get; set; } = 60;

	/// <summary>
	/// The maximum number of events to save in a single operation.
	/// </summary>
	[Range(1, 10_000)]
	public int MaxEventCountOnSave { get; set; } = 1000;

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
	/// Sets the suffix for writing events.
	/// </summary>
	/// <remarks>Changing this where data already exists will result in incomplete aggregates.</remarks>
	[Required]
	[StringLength(100, MinimumLength = 1)]
	[DefaultValue(DefaultEventPrefix)]
	public string EventPrefix { get; set; } = DefaultEventPrefix;

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
	/// uses the <see cref="IDistributedCache"/> during it's operations. Defaults to <see cref="SnapshotCachingOptions.GetAndStore"/>.
	/// </summary>
	[DefaultValue(SnapshotCachingOptions.GetAndStore)]
	public SnapshotCachingOptions CacheMode { get; set; } = SnapshotCachingOptions.GetAndStore;

	/// <summary>
	/// Gets or sets the sliding expiration applied to cached aggregate snapshots when no explicit cache
	/// options are supplied by the <see cref="EventStoreOperationContext"/>.
	/// </summary>
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
}
