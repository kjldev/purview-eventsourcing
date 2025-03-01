namespace Purview.EventSourcing.Aggregates.Exceptions;

/// <summary>
/// Indicates the <see cref="IAggregate"/> is in a locked state, and
/// cannot be modified or saved.
/// </summary>
/// <seealso cref="AggregateDetails.Locked"/>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
public sealed class LockedException : Exception
{
	public LockedException(string aggregateId, string? message = null)
		: base(message ?? CreateMessage(aggregateId))
	{
		AggregateId = aggregateId;
	}

	public LockedException(string aggregateId, Exception inner, string? message = null)
		: base(message ?? CreateMessage(aggregateId), inner)
	{
		AggregateId = aggregateId;
	}

	public string AggregateId { get; set; }

	static string CreateMessage(string aggregateId)
		=> $"The aggregate with Id '{aggregateId}' is locked for modifications.";
}
