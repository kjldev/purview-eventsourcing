namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregateLockedException(string idempotencyId)
	: Exception($"An attempt to save an aggregate that is currently locked was made, {nameof(idempotencyId)}: {idempotencyId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string IdempotencyId { get; set; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));
}
