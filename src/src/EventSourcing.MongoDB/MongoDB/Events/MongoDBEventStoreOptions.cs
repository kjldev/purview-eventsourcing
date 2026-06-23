using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing.MongoDB.Events;

public sealed class MongoDBEventStoreOptions
{
	public const string MongoDBEventStore = "EventStore:MongoDB";

	const bool DefaultRemoveDeletedFromCache = true;
	const int DefaultEventSuffixLength = 30;

	[Required]
	public string ConnectionString { get; set; } = default!;

	public string? ApplicationName { get; set; }

	[Required]
	// TODO: Regex
	public string Database { get; set; } = default!;

	// TODO: Regex
	public string? EventCollection { get; set; }

	// TODO: Regex
	public string? SnapshotCollection { get; set; }

	public string? ReplicaName { get; set; }

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
}
