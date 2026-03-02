using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Purview.EventSourcing.SqlServer.EntityFramework;

/// <summary>
/// Design-time factory for <see cref="EventStoreDbContext"/>.
/// Used by EF Core tools to generate migrations.
/// </summary>
public sealed class EventStoreDbContextDesignTimeFactory
	: IDesignTimeDbContextFactory<EventStoreDbContext>
{
	public EventStoreDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<EventStoreDbContext>();
		optionsBuilder.UseSqlServer(
			"Server=(localdb)\\mssqllocaldb;Database=EventStore_Design;Trusted_Connection=True;"
		);

		return new EventStoreDbContext(optionsBuilder.Options);
	}
}
