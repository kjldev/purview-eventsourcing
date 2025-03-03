namespace Purview.EventSourcing;

/// <summary>
/// Controls how to handle aggregates in a deleted state during get and save/ restore operations.
/// </summary>
public enum DeleteHandlingMode
{
	/// <summary>
	/// If the aggregate is marked as deleted, returns null. This is the default.
	/// </summary>
	ReturnsNull = 0,

	/// <summary>
	/// Should throw an exception.
	/// </summary>
	ThrowsException,

	/// <summary>
	/// Returns the aggregate in it's deleted state.
	/// </summary>
	ReturnsAggregate
}
