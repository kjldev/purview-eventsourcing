using Microsoft.EntityFrameworkCore;

namespace Purview.EventSourcing.SqlServer.Snapshot.EntityFramework;

/// <summary>
/// EF Core DbContext for the SQL Server snapshot store.
/// Provides model-first design with migrations for the snapshot table,
/// matching the same schema and indices as the ADO.NET auto-create path.
/// </summary>
public class SnapshotStoreDbContext : DbContext
{
	readonly string _schemaName;
	readonly string _tableName;

	/// <summary>
	/// All snapshot store rows.
	/// </summary>
	public DbSet<SnapshotStoreEntity> SnapshotEntities { get; set; } = default!;

	/// <summary>
	/// Creates a new <see cref="SnapshotStoreDbContext"/> with the specified options.
	/// Schema and table names default to "dbo" and "Snapshots".
	/// </summary>
	public SnapshotStoreDbContext(DbContextOptions<SnapshotStoreDbContext> options)
		: this(options, "dbo", "Snapshots")
	{
	}

	/// <summary>
	/// Creates a new <see cref="SnapshotStoreDbContext"/> with explicit schema and table names.
	/// </summary>
	public SnapshotStoreDbContext(DbContextOptions<SnapshotStoreDbContext> options, string schemaName, string tableName)
		: base(options)
	{
		_schemaName = schemaName;
		_tableName = tableName;
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		ArgumentNullException.ThrowIfNull(modelBuilder);

		modelBuilder.Entity<SnapshotStoreEntity>(entity =>
		{
			entity.ToTable(_tableName, _schemaName);

			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasMaxLength(450).IsRequired();
			entity.Property(e => e.AggregateType).HasMaxLength(450).IsRequired();
			entity.Property(e => e.Payload).HasColumnType("NVARCHAR(MAX)").IsRequired();

			// Covers: QueryByAggregateType (returns Payload via INCLUDE, avoids key lookup)
			entity.HasIndex(e => e.AggregateType)
				.HasDatabaseName($"IX_{_tableName}_AggregateType")
				.IncludeProperties(e => e.Payload);
		});
	}
}
