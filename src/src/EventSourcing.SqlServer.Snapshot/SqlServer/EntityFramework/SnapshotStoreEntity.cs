namespace Purview.EventSourcing.SqlServer.EntityFramework;

/// <summary>
/// EF Core entity model representing a row in the SQL Server snapshot store table.
/// Used for model-first migrations — maps to the same schema as the ADO.NET client.
/// </summary>
public sealed class SnapshotStoreEntity
{
	/// <summary>
	/// The aggregate identifier (primary key).
	/// </summary>
	public string Id { get; set; } = default!;

	/// <summary>
	/// Short name of the aggregate type (e.g. "Order").
	/// </summary>
	public string AggregateType { get; set; } = default!;

	/// <summary>
	/// JSON-serialized aggregate snapshot.
	/// </summary>
	public string Payload { get; set; } = default!;
}
