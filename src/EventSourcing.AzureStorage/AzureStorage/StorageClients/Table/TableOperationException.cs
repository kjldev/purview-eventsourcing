using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage.StorageClients.Table;

#pragma warning disable CA1032 // Implement standard exception constructors
public sealed class TableOperationException(ITableEntity entity, TableTransactionActionType actionType, Azure.Response response)
	: Exception($"Operation {actionType} failed with status {response.Status}.\n\tPartition Key: {entity.PartitionKey}\n\tRow Key: {entity.RowKey}")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public ITableEntity Entity { get; set; } = entity;

	public TableTransactionActionType ActionType { get; set; } = actionType;

	public Azure.Response Response { get; set; } = response;
}
