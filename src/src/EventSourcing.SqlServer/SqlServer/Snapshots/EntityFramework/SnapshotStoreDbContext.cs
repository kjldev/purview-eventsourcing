using Microsoft.EntityFrameworkCore;

namespace Purview.EventSourcing.SqlServer.Snapshots.EntityFramework;

/// <summary>
/// EF Core DbContext for the SQL Server snapshot store.
/// Provides model-first design with migrations for the snapshot table,
/// matching the same schema and indices as the ADO.NET auto-create path.
/// </summary>
/// <remarks>
/// Creates a new <see cref="SnapshotStoreDbContext"/> with explicit schema and table names.
/// </remarks>
public class SnapshotStoreDbContext(
	DbContextOptions<SnapshotStoreDbContext> options,
	string schemaName,
	string tableName
) : DbContext(options)
{
	readonly string _schemaName = schemaName;
	readonly string _tableName = tableName;

	/// <summary>
	/// All snapshot store rows.
	/// </summary>
	public DbSet<SnapshotStoreEntity> SnapshotEntities { get; set; } = default!;

	/// <summary>
	/// Creates a new <see cref="SnapshotStoreDbContext"/> with the specified options.
	/// Schema and table names default to "dbo" and "Snapshots".
	/// </summary>
	public SnapshotStoreDbContext(DbContextOptions<SnapshotStoreDbContext> options)
		: this(options, "dbo", "Snapshots") { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.Entity<SnapshotStoreEntity>(entity =>
		{
			entity.ToTable(_tableName, _schemaName);

			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasMaxLength(450).IsRequired();
			entity.Property(e => e.AggregateType).HasMaxLength(450).IsRequired();
			entity.Property(e => e.Payload).HasColumnType("json").IsRequired();

			// Covers: QueryByAggregateType (returns Payload via INCLUDE, avoids key lookup)
			entity
				.HasIndex(e => e.AggregateType)
				.HasDatabaseName($"IX_{_tableName}_AggregateType")
				.IncludeProperties(e => e.Payload);
		});
	}
}
