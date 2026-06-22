using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.SqlServer.Events;

namespace Purview.EventSourcing.SqlServer.Events;

public sealed class SqlServerEventStoreTransactionFactoryTests
{
	[Test]
	public async Task CreateSqlServerTransaction_GivenNullCorrelationId_UsesAmbientProviderValue()
	{
		var correlationIdProvider = Substitute.For<IEventStoreCorrelationIdProvider>();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		var factory = new SqlServerEventStoreTransactionFactory(correlationIdProvider);

		await using var transaction = factory.CreateSqlServerTransaction();

		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-sql-correlation");
	}

	[Test]
	public async Task CreateSqlServerTransaction_GivenExplicitCorrelationId_PrefersExplicitValueOverAmbientProvider()
	{
		var correlationIdProvider = Substitute.For<IEventStoreCorrelationIdProvider>();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		var factory = new SqlServerEventStoreTransactionFactory(correlationIdProvider);

		await using var transaction = factory.CreateSqlServerTransaction("explicit-sql-correlation");

		await Assert.That(transaction.CorrelationId).IsEqualTo("explicit-sql-correlation");
		correlationIdProvider.DidNotReceive().GetCorrelationId();
	}

	[Test]
	public async Task Create_GivenNullCorrelationId_ReturnsSqlServerTransaction()
	{
		var correlationIdProvider = Substitute.For<IEventStoreCorrelationIdProvider>();
		correlationIdProvider.GetCorrelationId().Returns("ambient-sql-correlation");
		var factory = new SqlServerEventStoreTransactionFactory(correlationIdProvider);

		await using var transaction = factory.Create();

		await Assert.That(transaction).IsTypeOf<SqlServerEventStoreTransaction>();
		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-sql-correlation");
	}

	[Test]
	public async Task AddEventSourcing_GivenNoSqlRegistration_UsesDefaultTransactionFactory()
	{
		var services = new ServiceCollection();
		services.AddEventSourcing();

		using var serviceProvider = services.BuildServiceProvider();
		var factory = serviceProvider.GetRequiredService<IEventStoreTransactionFactory>();

		await Assert.That(factory).IsTypeOf<EventStoreTransactionFactory>();
	}

	[Test]
	public async Task AddSqlServerEventStore_RegistersSqlServerSpecificFactoryWithoutReplacingDefaultFactory()
	{
		var services = new ServiceCollection();
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
}
