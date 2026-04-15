using Microsoft.EntityFrameworkCore;

namespace Purview.EventSourcing.SqlServer.EntityFramework;

/// <summary>
/// EF Core DbContext for the SQL Server event store.
/// Provides model-first design with migrations for the event store table,
/// matching the same schema and indices as the ADO.NET auto-create path.
/// </summary>
/// <remarks>
/// Creates a new <see cref="EventStoreDbContext"/> with explicit schema and table names.
/// </remarks>
public class EventStoreDbContext(DbContextOptions<EventStoreDbContext> options, string schemaName, string tableName) : DbContext(options)
{
	readonly string _schemaName = schemaName;
	readonly string _tableName = tableName;

	/// <summary>
	/// All event store rows (stream versions, events, idempotency markers, snapshots).
	/// </summary>
	public DbSet<EventStoreEntity> EventStoreEntities { get; set; } = default!;

	/// <summary>
	/// Creates a new <see cref="EventStoreDbContext"/> with the specified options.
	/// Schema and table names default to "dbo" and "EventStore".
	/// </summary>
	public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options)
		: this(options, "dbo", "EventStore") { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.Entity<EventStoreEntity>(entity =>
		{
			entity.ToTable(_tableName, _schemaName);

			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasMaxLength(450).IsRequired();
			entity.Property(e => e.EntityType).IsRequired();
			entity.Property(e => e.AggregateId).HasMaxLength(450).IsRequired();
			entity.Property(e => e.AggregateType).HasMaxLength(450).IsRequired();
			entity.Property(e => e.Version).HasDefaultValue(0).IsRequired();
			entity.Property(e => e.IsDeleted).HasDefaultValue(false).IsRequired();
			entity.Property(e => e.Payload).HasColumnType("json");
			entity.Property(e => e.EventType).HasMaxLength(450);
			entity.Property(e => e.IdempotencyId).HasMaxLength(450);
			entity.Property(e => e.Timestamp).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();

			// Covers: GetByAggregateIdAndEntityType, GetIdempotencyMarkers, DeleteByAggregateId
			entity
				.HasIndex(e => new { e.AggregateId, e.EntityType })
				.HasDatabaseName($"IX_{_tableName}_AggregateId_EntityType")
				.IncludeProperties(e => new
				{
					e.Version,
					e.IsDeleted,
					e.AggregateType,
					e.EventType,
					e.IdempotencyId,
					e.Timestamp,
				});

			// Covers: GetEventRange (AggregateId + EntityType=1 + Version range, ORDER BY Version)
			entity
				.HasIndex(e => new
				{
					e.AggregateId,
					e.EntityType,
					e.Version,
				})
				.HasDatabaseName($"IX_{_tableName}_EventRange")
				.HasFilter("[EntityType] = 1")
				.IncludeProperties(e => new
				{
					e.Payload,
					e.EventType,
					e.IdempotencyId,
					e.IsDeleted,
					e.AggregateType,
					e.Timestamp,
				});

			// Covers: GetAggregateIdsAsync (AggregateType + EntityType + optional IsDeleted filter)
			entity
				.HasIndex(e => new
				{
					e.AggregateType,
					e.EntityType,
					e.IsDeleted,
				})
				.HasDatabaseName($"IX_{_tableName}_AggregateType_EntityType")
				.IncludeProperties(e => new { e.AggregateId });
		});
	}
}
