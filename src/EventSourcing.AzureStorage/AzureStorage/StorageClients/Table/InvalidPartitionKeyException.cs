namespace Purview.EventSourcing.AzureStorage.StorageClients.Table;

#pragma warning disable CA1032 // Implement standard exception constructors
public sealed class InvalidPartitionKeyException(string existingPartitionKey, string invalidPartitionKey)
	: Exception($"Batched entities must have matching partition keys.\n\nExpected: {existingPartitionKey}\nPassed: {invalidPartitionKey}")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string InvalidPartitionKey { get; } = invalidPartitionKey;

	public string ExistingPartitionKey { get; } = existingPartitionKey;
}
