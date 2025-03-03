namespace Purview.EventSourcing;

/// <summary>
/// Controls how the cache is used in <see cref="IEventStore{T}"/> operations.
/// </summary>
[Flags]
public enum EventStoreCachingOptions
{
	/// <summary>
	/// Disables the cache completely.
	/// </summary>
	None = 0,

	/// <summary>
	/// Attempts to get snapshot from the cache, before using the underlaying storage.
	/// </summary>
	GetFromCache = 1,

	/// <summary>
	/// Uses the cache for storing snapshots after a successful save operation.
	/// </summary>
	StoreInCache = 2,

	/// <summary>
	/// Enables the cache for both <see cref="GetFromCache"/> and <see cref="StoreInCache"/>
	/// operations.
	/// </summary>
	GetAndStore = GetFromCache | StoreInCache
}
