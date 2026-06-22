using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Fixtures.SqlServer;

namespace Purview.EventSourcing.SqlServer.Events;

[ClassDataSource<SqlServerEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class SqlServerEventStoreTransactionIntegrationTests(SqlServerEventStoreFixture fixture)
{
	[Test]
	public async Task CommitAsync_GivenEnlistedRawSqlOperation_CommitsAggregateAndRawSqlTogether(
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<PersistenceAggregate>();
		var aggregateId = $"{Guid.NewGuid():N}";
		var tableName = $"TransactionAudit_{Guid.NewGuid():N}";

		await EnsureAuditTableExistsAsync(fixture.ConnectionString, tableName, cancellationToken);

		var aggregate = await eventStore.CreateAsync(aggregateId, cancellationToken);
		aggregate.AppendString("raw-sql");

		var factory = new SqlServerEventStoreTransactionFactory(new FixedCorrelationIdProvider("sql-raw"));
		await using var transaction = factory.CreateSqlServerTransaction();
		transaction.Enlist(aggregate, eventStore);
		transaction.Enlist(
			async (connection, sqlTransaction, token) =>
			{
				var quotedTableName = QuoteTableName(tableName);
#pragma warning disable CA2100
				await using var command = new SqlCommand(
					$"INSERT INTO {quotedTableName} ([CorrelationId], [Value]) VALUES (@correlationId, @value)",
					connection,
					sqlTransaction
				);
#pragma warning restore CA2100
				command.Parameters.AddWithValue("@correlationId", "sql-raw");
				command.Parameters.AddWithValue("@value", "raw-sql");
				await command.ExecuteNonQueryAsync(token);
			}
		);

		var result = await transaction.CommitAsync(cancellationToken);
		var savedAggregate = await eventStore.GetAsync(aggregateId, null, cancellationToken);
		var rowCount = await GetAuditRowCountAsync(fixture.ConnectionString, tableName, cancellationToken);

		await Assert.That(result.Success).IsTrue();
		await Assert.That(savedAggregate).IsNotNull();
		await Assert.That(savedAggregate!.StringProperty).Contains("raw-sql");
		await Assert.That(rowCount).IsEqualTo(1);
	}

	[Test]
	public async Task CommitAsync_GivenEnlistedEfOperation_CommitsAggregateAndEfSaveTogether(
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<PersistenceAggregate>();
		var aggregateId = $"{Guid.NewGuid():N}";
		var tableName = $"TransactionAudit_{Guid.NewGuid():N}";

		await EnsureAuditTableExistsAsync(fixture.ConnectionString, tableName, cancellationToken);

		var aggregate = await eventStore.CreateAsync(aggregateId, cancellationToken);
		aggregate.AppendString("ef");

		var factory = new SqlServerEventStoreTransactionFactory(new FixedCorrelationIdProvider("sql-ef"));
		await using var transaction = factory.CreateSqlServerTransaction();
		transaction.Enlist(aggregate, eventStore);
		transaction.Enlist<TransactionAuditDbContext>(
			connection => new TransactionAuditDbContext(connection, tableName),
			async (dbContext, token) =>
			{
				dbContext.Entries.Add(new TransactionAuditEntry { CorrelationId = "sql-ef", Value = "ef" });
				await dbContext.SaveChangesAsync(token);
			}
		);

		var result = await transaction.CommitAsync(cancellationToken);
		var savedAggregate = await eventStore.GetAsync(aggregateId, null, cancellationToken);
		var rowCount = await GetAuditRowCountAsync(fixture.ConnectionString, tableName, cancellationToken);

		await Assert.That(result.Success).IsTrue();
		await Assert.That(savedAggregate).IsNotNull();
		await Assert.That(savedAggregate!.StringProperty).Contains("ef");
		await Assert.That(rowCount).IsEqualTo(1);
	}

	[Test]
	public async Task CommitAsync_GivenEnlistedSqlOperationThrows_RollsBackAggregateAndSqlInsert(
		CancellationToken cancellationToken
	)
	{
		var eventStore = fixture.CreateEventStore<PersistenceAggregate>();
		var aggregateId = $"{Guid.NewGuid():N}";
		var tableName = $"TransactionAudit_{Guid.NewGuid():N}";

		await EnsureAuditTableExistsAsync(fixture.ConnectionString, tableName, cancellationToken);

		var aggregate = await eventStore.CreateAsync(aggregateId, cancellationToken);
		aggregate.AppendString("rollback");

		var factory = new SqlServerEventStoreTransactionFactory(new FixedCorrelationIdProvider("sql-rollback"));
		await using var transaction = factory.CreateSqlServerTransaction();
		transaction.Enlist(aggregate, eventStore);
		transaction.Enlist(
			async (connection, sqlTransaction, token) =>
			{
				var quotedTableName = QuoteTableName(tableName);
#pragma warning disable CA2100
				await using var command = new SqlCommand(
					$"INSERT INTO {quotedTableName} ([CorrelationId], [Value]) VALUES (@correlationId, @value)",
					connection,
					sqlTransaction
				);
#pragma warning restore CA2100
				command.Parameters.AddWithValue("@correlationId", "sql-rollback");
				command.Parameters.AddWithValue("@value", "rollback");
				await command.ExecuteNonQueryAsync(token);
				throw new InvalidOperationException("forced-failure");
			}
		);

		var result = await transaction.CommitAsync(cancellationToken);
		var savedAggregate = await eventStore.GetAsync(aggregateId, null, cancellationToken);
		var rowCount = await GetAuditRowCountAsync(fixture.ConnectionString, tableName, cancellationToken);

		await Assert.That(result.Success).IsFalse();
		await Assert.That(result.Results).Count().IsEqualTo(1);
		await Assert.That(result.Results[0].Error).IsTypeOf<InvalidOperationException>();
		await Assert.That(savedAggregate).IsNull();
		await Assert.That(rowCount).IsEqualTo(0);
	}

	static async Task EnsureAuditTableExistsAsync(
		string connectionString,
		string tableName,
		CancellationToken cancellationToken
	)
	{
		var quotedTableName = QuoteTableName(tableName);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"""
			IF OBJECT_ID(N'{quotedTableName}', N'U') IS NULL
			BEGIN
				CREATE TABLE {quotedTableName}(
					[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
					[CorrelationId] NVARCHAR(128) NOT NULL,
					[Value] NVARCHAR(128) NOT NULL
				);
			END
			""",
			connection
		);
#pragma warning restore CA2100
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	static async Task<int> GetAuditRowCountAsync(
		string connectionString,
		string tableName,
		CancellationToken cancellationToken
	)
	{
		var quotedTableName = QuoteTableName(tableName);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);
#pragma warning disable CA2100
		await using var command = new SqlCommand($"SELECT COUNT(1) FROM {quotedTableName}", connection);
#pragma warning restore CA2100
		var count = await command.ExecuteScalarAsync(cancellationToken);
		return Convert.ToInt32(count, CultureInfo.InvariantCulture);
	}

	static string QuoteTableName(string tableName)
	{
		if (!Regex.IsMatch(tableName, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
			throw new ArgumentException("Audit table name contains unsupported characters.", nameof(tableName));

		return $"[dbo].[{tableName}]";
	}

	sealed class FixedCorrelationIdProvider(string correlationId) : IEventStoreCorrelationIdProvider
	{
		public string? GetCorrelationId() => correlationId;
	}

	sealed class TransactionAuditDbContext(SqlConnection connection, string tableName) : DbContext
	{
		readonly string _tableName = tableName;

		public DbSet<TransactionAuditEntry> Entries => Set<TransactionAuditEntry>();

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
			optionsBuilder.UseSqlServer(connection);

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<TransactionAuditEntry>(entity =>
			{
				entity.ToTable(_tableName, "dbo");
				entity.HasKey(x => x.Id);
				entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
				entity.Property(x => x.Value).HasMaxLength(128).IsRequired();
			});
		}
	}

	sealed class TransactionAuditEntry
	{
		public int Id { get; set; }
		public string CorrelationId { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}
}
