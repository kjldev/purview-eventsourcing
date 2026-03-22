using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Cosmos;

namespace Purview.EventSourcing.CosmosDb;

public class CosmosDbOptions
{
	public static int DefaultRequestTimeout { get; set; } = 5;

	public static int DefaultDatabaseThroughput { get; set; } = MinimumThroughput;

	public static int DefaultContainerThroughput { get; set; } = MinimumThroughput;

	const int MinimumThroughput = 400;

	[Required]
	public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Direct;

	[Required]
	public string ConnectionString { get; set; } = default!;

	[Required]
	public string Database { get; set; } = default!;

	[Required]
	public string Container { get; set; } = default!;

	[Range(1, 120000)]
	public int? RequestTimeoutInSeconds { get; set; } = DefaultRequestTimeout;

	/// <summary>
	/// This is only used when creating a non-existent database, it does not modify existing databases.
	/// </summary>
	[Range(MinimumThroughput, int.MaxValue)]
	public int DatabaseThroughput { get; set; } = DefaultDatabaseThroughput;

	/// <summary>
	/// This is only used when creating a non-existent collection, it does not modify existing collections.
	/// </summary>
	[Range(MinimumThroughput, int.MaxValue)]
	public int ContainerThroughput { get; set; } = DefaultContainerThroughput;

	[Required]
	[RegularExpression("^[/].+$")]
	public string PartitionKeyPath { get; set; } = default!;

	/// <summary>
	/// WARNING: This is used when connecting to emulators only, i.e. for testing purposes.
	/// </summary>
	public bool IgnoreSSLWarnings { get; set; }

	[Required]
	public CosmosDbIndexOptions IndexOptions { get; set; } = new();
}
