using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Purview.EventSourcing.SqlServer.Snapshot.EntityFramework;

/// <summary>
/// Design-time factory for <see cref="SnapshotStoreDbContext"/>.
/// Used by EF Core tools to generate migrations.
/// </summary>
public sealed class SnapshotStoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SnapshotStoreDbContext>
{
	public SnapshotStoreDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<SnapshotStoreDbContext>();
		optionsBuilder.UseSqlServer(
			"Server=(localdb)\\mssqllocaldb;Database=SnapshotStore_Design;Trusted_Connection=True;"
		);

		return new SnapshotStoreDbContext(optionsBuilder.Options);
	}
}
