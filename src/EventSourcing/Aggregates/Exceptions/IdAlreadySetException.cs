namespace Purview.EventSourcing.Aggregates.Exceptions;

/// <summary>
/// Indicates the <see cref="AggregateDetails.Id"/> was already set.
/// </summary>
public sealed class IdAlreadySetException : Exception
{
	public IdAlreadySetException() { }

	public IdAlreadySetException(string message) : base(message) { }

	public IdAlreadySetException(string message, Exception inner) : base(message, inner) { }
}
