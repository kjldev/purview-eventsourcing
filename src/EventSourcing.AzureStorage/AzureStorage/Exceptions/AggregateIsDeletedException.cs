namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class AggregateIsDeletedException(string aggregateId)
	: Exception($"Invalid operation against an aggregate (Id: {aggregateId}) that has been deleted.")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));
}
