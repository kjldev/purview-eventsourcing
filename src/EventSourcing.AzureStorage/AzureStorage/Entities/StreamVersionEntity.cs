using Azure;
using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage.Entities;

public sealed class StreamVersionEntity : ITableEntity
{
	public bool IsDeleted { get; set; }

	public string AggregateType { get; set; } = default!;

	/// <summary>
	/// This is the most recently saved version of the aggregate.
	/// </summary>
	public int Version { get; set; }

	public string PartitionKey { get; set; } = default!;

	public string RowKey { get; set; } = default!;

	public DateTimeOffset? Timestamp { get; set; }

	public ETag ETag { get; set; }
}
