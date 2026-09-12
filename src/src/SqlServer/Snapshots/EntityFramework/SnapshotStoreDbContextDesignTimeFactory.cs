using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Purview.EventSourcing.SqlServer.Snapshots.EntityFramework;

/// <summary>
/// Design-time factory for <see cref="SnapshotStoreDBContext"/>.
/// Used by EF Core tools to generate migrations.
/// </summary>
public sealed class SnapshotStoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SnapshotStoreDBContext>
{
	///<inheritdoc/>
	public SnapshotStoreDBContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<SnapshotStoreDBContext> optionsBuilder = new();
		optionsBuilder.UseSqlServer(
			"Server=(localdb)\\mssqllocaldb;Database=SnapshotStore_Design;Trusted_Connection=True;"
		);

		return new SnapshotStoreDBContext(optionsBuilder.Options);
	}
}
