using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Purview.EventSourcing.Postgres.Events.EntityFramework;

/// <summary>
/// Design-time factory for <see cref="EventStoreDbContext"/>.
/// Used by EF Core tools to generate migrations.
/// </summary>
public sealed class EventStoreDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EventStoreDbContext>
{
	/// <summary>
	/// Creates a new <see cref="EventStoreDbContext"/> for design-time tooling.
	/// </summary>
	/// <param name="args">Command-line arguments passed by EF Core design-time tools.</param>
	/// <returns>A new <see cref="EventStoreDbContext"/> instance.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Naming",
		"PDS0004:Use correct acronym capitalization",
		Justification = "Matches source."
	)]
	public EventStoreDbContext CreateDbContext(string[] args)
	{
		DbContextOptionsBuilder<EventStoreDbContext> optionsBuilder = new();
		optionsBuilder.UseNpgsql("Host=localhost;Database=eventstore_design;Username=postgres;Password=postgres");

		return new EventStoreDbContext(optionsBuilder.Options);
	}
}
