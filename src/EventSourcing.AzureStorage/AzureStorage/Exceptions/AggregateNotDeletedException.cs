namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregateNotDeletedException(string aggregateId)
	: Exception($"An attempt to get an aggregate that has not been deleted, aggregate Id: {aggregateId}.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));
}
