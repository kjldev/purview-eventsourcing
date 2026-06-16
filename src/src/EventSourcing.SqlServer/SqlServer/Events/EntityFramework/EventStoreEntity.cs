namespace Purview.EventSourcing.SqlServer.Events.EntityFramework;

/// <summary>
/// EF Core entity model representing a row in the SQL Server event store table.
/// Used for model-first migrations — maps to the same schema as the ADO.NET client.
/// </summary>
public sealed class EventStoreEntity
{
	/// <summary>
	/// Composite primary key (e.g. "s_Type_Id", "e_Type_Id_00001", "snap_Type_Id").
	/// </summary>
	public string Id { get; set; } = default!;

	/// <summary>
	/// Discriminator: 0 = StreamVersion, 1 = Event, 2 = IdempotencyMarker, 3 = Snapshot.
	/// </summary>
	public int EntityType { get; set; }

	/// <summary>
	/// The aggregate identifier this row belongs to.
	/// </summary>
	public string AggregateId { get; set; } = default!;

	/// <summary>
	/// Short name of the aggregate type (e.g. "Order").
	/// </summary>
	public string AggregateType { get; set; } = default!;

	/// <summary>
	/// The event or stream version number.
	/// </summary>
	public int Version { get; set; }

	/// <summary>
	/// Whether this aggregate has been soft-deleted.
	/// </summary>
	public bool IsDeleted { get; set; }

	/// <summary>
	/// JSON-serialized event or snapshot payload (null for stream version and idempotency markers).
	/// </summary>
	public string? Payload { get; set; }

	/// <summary>
	/// The event type name (null for non-event rows).
	/// </summary>
	public string? EventType { get; set; }

	/// <summary>
	/// The idempotency marker identifier (null for non-idempotency rows).
	/// </summary>
	public string? IdempotencyId { get; set; }

	/// <summary>
	/// UTC timestamp of when this row was created or last modified.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; }
}
