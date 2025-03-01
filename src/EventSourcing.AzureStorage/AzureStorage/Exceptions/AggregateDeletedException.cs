namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregateDeletedException(string aggregateId, string idempotencyId)
	: Exception($"An attempt to save an aggregate that has been deleted, aggregate Id: {aggregateId}, {nameof(IdempotencyId)}: {idempotencyId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));

	public string IdempotencyId { get; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
}
