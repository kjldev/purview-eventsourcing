using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Purview.EventSourcing.SqlServer.Events.EntityFramework;

/// <summary>
/// Design-time factory for <see cref="EventStoreDbContext"/>.
/// Used by EF Core tools to generate migrations.
/// </summary>
public sealed class EventStoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EventStoreDbContext>
{
	///<inheritdoc/>
	public EventStoreDbContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<EventStoreDbContext> optionsBuilder = new();
		optionsBuilder.UseSqlServer(
			"Server=(localdb)\\mssqllocaldb;Database=EventStore_Design;Trusted_Connection=True;"
		);

		return new EventStoreDbContext(optionsBuilder.Options);
	}
}
