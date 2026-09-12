using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Purview.EventSourcing.SqlServer.Snapshots;

public sealed class SqlServerSnapshotServiceCollectionExtensionsTests
{
	[Test]
	public async Task AddSqlServerSnapshotQueryableEventStore_GivenInvalidJsonIndexConfiguration_ResolvingOptionsThrowsValidationException()
	{
		ServiceCollection services = new();
		services.AddSingleton<IConfiguration>(
			new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?>
					{
						["ConnectionStrings:eventstore-snapshots-sqlserver"] =
							"Server=.;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;",
					}
				)
				.Build()
		);
		services.AddSqlServerSnapshotQueryableEventStore();
		services.Configure<SqlServerSnapshotEventStoreOptions>(options =>
		{
			options.JsonIndexOptions.Enabled = true;
			options.JsonIndexOptions.Indexes =
			[
				new SqlServerJsonIndexDefinition { JsonPath = "$.StringProperty", IncludeColumns = ["Version"] },
			];
		});

		using var serviceProvider = services.BuildServiceProvider();

		var exception = await Assert
			.That(() => serviceProvider.GetRequiredService<IOptions<SqlServerSnapshotEventStoreOptions>>().Value)
			.Throws<OptionsValidationException>();

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains("unsupported column");
	}
}
