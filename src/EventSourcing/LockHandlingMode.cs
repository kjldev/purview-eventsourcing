namespace Purview.EventSourcing;

/// <summary>
/// Controls how to handle aggregates in a locked state with save operations.
/// </summary>
public enum LockHandlingMode
{
	/// <summary>
	/// If the aggregate is marked as locked, save operations throw an exception. This is the default.
	/// </summary>
	ThrowsException,

	/// <summary>
	/// If the aggregate is marked as locked, save operations return false.
	/// </summary>
	ReturnsFalse = 1
}
