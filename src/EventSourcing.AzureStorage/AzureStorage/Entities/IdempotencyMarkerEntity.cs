using Azure;
using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage.Entities;

public sealed class IdempotencyMarkerEntity : ITableEntity
{
	public IdempotencyMarkerEntity(string partitionKey, string rowKey)
	{
		PartitionKey = partitionKey;
		RowKey = rowKey;
	}

	public IdempotencyMarkerEntity()
	{
	}

	public string Events { get; set; } = default!;

	public string PartitionKey { get; set; } = default!;

	public string RowKey { get; set; } = default!;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }
}
