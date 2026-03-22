namespace Purview.EventSourcing.MongoDB.Exceptions;

#pragma warning disable CA1032 // Implement standard exception constructors
public class MissingAggregateIdException(string idempotencyId)
	: Exception($"An attempt to save an aggregate is a missing Id was made, {nameof(idempotencyId)}: {idempotencyId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string IdempotencyId { get; set; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
}
