using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Purview.EventSourcing.SqlServer.Events;

public sealed class SqlServerEventStoreTransactionFactoryTests
{
	[Test]
	public async Task CreateSqlServerTransaction_GivenNullCorrelationId_UsesAmbientProviderValue()
	{
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		SqlServerEventStoreTransactionFactory factory = new(correlationIdProvider);

		await using var transaction = factory.CreateSqlServerTransaction();

		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-sql-correlation");
	}

	[Test]
	public async Task CreateSqlServerTransaction_GivenExplicitCorrelationId_PrefersExplicitValueOverAmbientProvider()
	{
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		SqlServerEventStoreTransactionFactory factory = new(correlationIdProvider);

		await using var transaction = factory.CreateSqlServerTransaction("explicit-sql-correlation");

		await Assert.That(transaction.CorrelationId).IsEqualTo("explicit-sql-correlation");
		correlationIdProvider.GetCorrelationId().WasNeverCalled();
	}

	[Test]
	public async Task Create_GivenNullCorrelationId_ReturnsSqlServerTransaction()
	{
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		SqlServerEventStoreTransactionFactory factory = new(correlationIdProvider);

		await using var transaction = factory.Create();

		await Assert.That(transaction).IsTypeOf<SqlServerEventStoreTransaction>();
		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-sql-correlation");
	}

	[Test]
	public async Task AddEventSourcing_GivenNoSqlRegistration_UsesDefaultTransactionFactory()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();

		using var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IEventStoreTransactionFactory>();

		await Assert.That(factory).IsTypeOf<EventStoreTransactionFactory>();
	}

	[Test]
	public async Task AddSqlServerEventStore_RegistersSqlServerSpecificFactoryWithoutReplacingDefaultFactory()
	{
		ServiceCollection services = new();
		services.AddSingleton<IConfiguration>(
			new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?>
					{
						["ConnectionStrings:eventstore-sqlserver"] =
							"Server=.;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;",
					}
				)
				.Build()
		);

		services.AddSqlServerEventStore();

		using var serviceProvider = services.BuildServiceProvider();
		var sqlFactory = serviceProvider.GetRequiredService<ISqlServerEventStoreTransactionFactory>();
		var defaultFactory = serviceProvider.GetRequiredService<IEventStoreTransactionFactory>();

		await Assert.That(sqlFactory).IsTypeOf<SqlServerEventStoreTransactionFactory>();
		await Assert.That(defaultFactory).IsTypeOf<EventStoreTransactionFactory>();
	}

	[Test]
	public async Task AddSqlServerEventStore_GivenInvalidJsonIndexConfiguration_ResolvingOptionsThrowsValidationException()
	{
		ServiceCollection services = new();
		services.AddSingleton<IConfiguration>(
			new ConfigurationBuilder()
				.AddInMemoryCollection(
					new Dictionary<string, string?>
					{
						["ConnectionStrings:eventstore-sqlserver"] =
							"Server=.;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;",
					}
				)
				.Build()
		);
		services.AddSqlServerEventStore();
		services.Configure<SqlServerEventStoreOptions>(options =>
		{
			options.JsonIndexOptions.Enabled = true;
			options.JsonIndexOptions.Indexes = [new SqlServerJsonIndexDefinition { JsonPath = "invalid-path" }];
		});

		using var serviceProvider = services.BuildServiceProvider();

		var exception = await Assert
			.That(() => serviceProvider.GetRequiredService<IOptions<SqlServerEventStoreOptions>>().Value)
			.Throws<OptionsValidationException>();

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains("JsonPath");
	}
}
