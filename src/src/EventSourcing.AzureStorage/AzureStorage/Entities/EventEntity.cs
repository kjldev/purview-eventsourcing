using Azure;
using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage.Entities;

public sealed class EventEntity : ITableEntity
{
	public string Payload { get; set; } = default!;

	public string EventType { get; set; } = default!;

	public string IdempotencyId { get; set; } = default!;

	public string PartitionKey { get; set; } = default!;

	public string RowKey { get; set; } = default!;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }
}
