namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregatedNotAlreadyDeletedException(string aggregateId, string idempotencyId)
	: Exception($"An attempt to restore an aggregate (Id: {aggregateId}) that is not deleted was made.\n\n\tIdempotencyId: {idempotencyId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));

	public string IdempotencyId { get; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
}
