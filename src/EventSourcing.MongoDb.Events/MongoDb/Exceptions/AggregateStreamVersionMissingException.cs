namespace Purview.EventSourcing.MongoDB.Exceptions;

#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregateStreamVersionMissingException(string aggregateId, string idempotencyId)
	: Exception($"An attempt to delete an aggregate (Id: {aggregateId}) that doesn't a stream version deleted was made. {nameof(idempotencyId)}: {idempotencyId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));

	public string IdempotencyId { get; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
}
