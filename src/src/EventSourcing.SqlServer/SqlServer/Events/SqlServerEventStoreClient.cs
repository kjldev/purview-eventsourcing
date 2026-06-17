using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Purview.EventSourcing.SqlServer.Events.EntityFramework;

namespace Purview.EventSourcing.SqlServer.Events;

sealed partial class SqlServerEventStoreClient
{
	static readonly ConcurrentDictionary<string, SemaphoreSlim> EnsureTableLocks = new(StringComparer.Ordinal);
	static readonly ConcurrentDictionary<string, byte> EnsuredTables = new(StringComparer.Ordinal);

	readonly SqlServerEventStoreOptions _options;
	readonly string _tableEnsureKey;

	public SqlServerEventStoreClient(SqlServerEventStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		ValidateIdentifier(_options.SchemaName);
		ValidateIdentifier(_options.TableName);
		_tableEnsureKey = $"{_options.ConnectionString}|{_options.SchemaName}|{_options.TableName}";
	}

	public Task EnsureTableExistsAsync(CancellationToken cancellationToken = default) =>
		EnsureTableIfEnabledAsync(cancellationToken);

	public Task EnsureTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(connection);
		return EnsureTableIfEnabledAsync(connection, transaction: null, cancellationToken);
	}

	public async Task InsertAsync(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		context.EventStoreEntities.Add(
			CreateEntity(
				id,
				entityType,
				aggregateId,
				aggregateType,
				version,
				isDeleted,
				payload,
				eventType,
				idempotencyId,
				timestamp
			)
		);
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task InsertBatchAsync(List<RowData> rows, CancellationToken cancellationToken = default)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		context.EventStoreEntities.AddRange(rows.Select(ToEntity));
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task UpsertAsync(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		await UpsertCoreAsync(
			context,
			id,
			entityType,
			aggregateId,
			aggregateType,
			version,
			isDeleted,
			payload,
			eventType,
			idempotencyId,
			timestamp,
			cancellationToken
		);
	}

	public async Task UpsertAsync(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		SqlConnection connection,
		SqlTransaction transaction,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(transaction);
		await EnsureTableIfEnabledAsync(connection, transaction, cancellationToken);
		await using var context = CreateContext(connection, transaction);
		await UpsertCoreAsync(
			context,
			id,
			entityType,
			aggregateId,
			aggregateType,
			version,
			isDeleted,
			payload,
			eventType,
			idempotencyId,
			timestamp,
			cancellationToken
		);
	}

	public async Task UpsertWithBatchAsync(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		List<RowData> additionalInserts,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
		await UpsertCoreAsync(
			context,
			id,
			entityType,
			aggregateId,
			aggregateType,
			version,
			isDeleted,
			payload,
			eventType,
			idempotencyId,
			timestamp,
			cancellationToken
		);

		context.EventStoreEntities.AddRange(additionalInserts.Select(ToEntity));
		await context.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
	}

	public async Task UpsertWithBatchAsync(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		List<RowData> additionalInserts,
		SqlConnection connection,
		SqlTransaction transaction,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(transaction);
		await EnsureTableIfEnabledAsync(connection, transaction, cancellationToken);
		await using var context = CreateContext(connection, transaction);
		await UpsertCoreAsync(
			context,
			id,
			entityType,
			aggregateId,
			aggregateType,
			version,
			isDeleted,
			payload,
			eventType,
			idempotencyId,
			timestamp,
			cancellationToken
		);

		context.EventStoreEntities.AddRange(additionalInserts.Select(ToEntity));
		await context.SaveChangesAsync(cancellationToken);
	}

	public async Task<bool> DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var entity = await context.EventStoreEntities.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (entity is null)
			return false;

		context.EventStoreEntities.Remove(entity);
		return await context.SaveChangesAsync(cancellationToken) > 0;
	}

	public async Task<int> DeleteByAggregateIdAsync(string aggregateId, CancellationToken cancellationToken = default)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var entities = await context
			.EventStoreEntities.Where(x => x.AggregateId == aggregateId)
			.ToListAsync(cancellationToken);
		if (entities.Count == 0)
			return 0;

		context.EventStoreEntities.RemoveRange(entities);
		await context.SaveChangesAsync(cancellationToken);
		return entities.Count;
	}

	public async Task<RowData?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var entity = await context
			.EventStoreEntities.AsNoTracking()
			.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		return entity is null ? null : ToRow(entity);
	}

	public async Task<RowData?> GetByIdAsync(
		string id,
		SqlConnection connection,
		SqlTransaction? transaction,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(connection);
		await EnsureTableIfEnabledAsync(connection, transaction, cancellationToken);
		await using var context = CreateContext(connection, transaction);
		var entity = await context
			.EventStoreEntities.AsNoTracking()
			.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		return entity is null ? null : ToRow(entity);
	}

	public async Task<RowData?> GetByAggregateIdAndEntityTypeAsync(
		string aggregateId,
		int entityType,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var entity = await context
			.EventStoreEntities.AsNoTracking()
			.SingleOrDefaultAsync(x => x.AggregateId == aggregateId && x.EntityType == entityType, cancellationToken);
		return entity is null ? null : ToRow(entity);
	}

	public async Task<RowData?> GetByAggregateIdAndEntityTypeAsync(
		string aggregateId,
		int entityType,
		SqlConnection connection,
		SqlTransaction? transaction,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(connection);
		await EnsureTableIfEnabledAsync(connection, transaction, cancellationToken);
		await using var context = CreateContext(connection, transaction);
		var entity = await context
			.EventStoreEntities.AsNoTracking()
			.SingleOrDefaultAsync(x => x.AggregateId == aggregateId && x.EntityType == entityType, cancellationToken);
		return entity is null ? null : ToRow(entity);
	}

	public async IAsyncEnumerable<RowData> GetEventRangeAsync(
		string aggregateId,
		int versionFrom,
		int versionTo,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var events = context
			.EventStoreEntities.AsNoTracking()
			.Where(x =>
				x.AggregateId == aggregateId && x.EntityType == 1 && x.Version >= versionFrom && x.Version <= versionTo
			)
			.OrderBy(x => x.Version)
			.AsAsyncEnumerable();

		await foreach (var entity in events.WithCancellation(cancellationToken))
			yield return ToRow(entity);
	}

	public async Task<List<string>> GetIdempotencyMarkerIdsByAggregateIdAsync(
		string aggregateId,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		return await context
			.EventStoreEntities.AsNoTracking()
			.Where(x => x.AggregateId == aggregateId && x.EntityType == 2)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);
	}

	public async IAsyncEnumerable<string> GetAggregateIdsByTypeAsync(
		string aggregateType,
		bool includeDeleted,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);
		await using var context = CreateContext();
		var aggregateIds = context
			.EventStoreEntities.AsNoTracking()
			.Where(x => x.AggregateType == aggregateType && x.EntityType == 0 && (includeDeleted || !x.IsDeleted))
			.Select(x => x.AggregateId)
			.AsAsyncEnumerable();

		await foreach (var aggregateId in aggregateIds.WithCancellation(cancellationToken))
			yield return aggregateId;
	}

	async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
	{
		if (_options.AutoCreateTable)
			await EnsureTableExistsAsync(cancellationToken);
	}

	async Task EnsureTableIfEnabledAsync(CancellationToken cancellationToken)
	{
		if (!_options.AutoCreateTable || IsTableEnsured())
			return;

		var tableLock = EnsureTableLocks.GetOrAdd(_tableEnsureKey, static _ => new SemaphoreSlim(1, 1));
		await tableLock.WaitAsync(cancellationToken);
		try
		{
			if (IsTableEnsured())
				return;

			try
			{
				await using var context = CreateContext();
				await CreateStorageTablesWithEfAsync(context, cancellationToken);
			}
			catch (Exception ex) when (IsDuplicateTableCreateError(ex, _options.TableName))
			{
				// Another writer won the table-creation race for this table.
			}

			MarkTableEnsured();
		}
		finally
		{
			tableLock.Release();
		}
	}

	async Task EnsureTableIfEnabledAsync(
		SqlConnection connection,
		SqlTransaction? transaction,
		CancellationToken cancellationToken
	)
	{
		if (!_options.AutoCreateTable || IsTableEnsured())
			return;

		var tableLock = EnsureTableLocks.GetOrAdd(_tableEnsureKey, static _ => new SemaphoreSlim(1, 1));
		await tableLock.WaitAsync(cancellationToken);
		try
		{
			if (IsTableEnsured())
				return;

			try
			{
				await using var context = CreateContext(connection, transaction);
				await CreateStorageTablesWithEfAsync(context, cancellationToken);
			}
			catch (Exception ex) when (IsDuplicateTableCreateError(ex, _options.TableName))
			{
				// Another writer won the table-creation race for this table.
			}

			MarkTableEnsured();
		}
		finally
		{
			tableLock.Release();
		}
	}

	EventStoreDbContext CreateContext()
	{
		var optionsBuilder = new DbContextOptionsBuilder<EventStoreDbContext>();
		var commandTimeout = Math.Max(1, _options.TimeoutInSeconds ?? 60);
		optionsBuilder.UseSqlServer(_options.ConnectionString, sql => sql.CommandTimeout(commandTimeout));
		return new EventStoreDbContext(optionsBuilder.Options, _options.SchemaName, _options.TableName);
	}

	EventStoreDbContext CreateContext(SqlConnection connection, SqlTransaction? transaction)
	{
		var optionsBuilder = new DbContextOptionsBuilder<EventStoreDbContext>();
		var commandTimeout = Math.Max(1, _options.TimeoutInSeconds ?? 60);
		optionsBuilder.UseSqlServer(connection, sql => sql.CommandTimeout(commandTimeout));
		var context = new EventStoreDbContext(optionsBuilder.Options, _options.SchemaName, _options.TableName);
		if (transaction is not null)
			context.Database.UseTransaction(transaction);
		return context;
	}

	static async Task UpsertCoreAsync(
		EventStoreDbContext context,
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp,
		CancellationToken cancellationToken
	)
	{
		var entity = await context.EventStoreEntities.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
		if (entity is null)
		{
			context.EventStoreEntities.Add(
				CreateEntity(
					id,
					entityType,
					aggregateId,
					aggregateType,
					version,
					isDeleted,
					payload,
					eventType,
					idempotencyId,
					timestamp
				)
			);
		}
		else
		{
			entity.EntityType = entityType;
			entity.AggregateId = aggregateId;
			entity.AggregateType = aggregateType;
			entity.Version = version;
			entity.IsDeleted = isDeleted;
			entity.Payload = payload;
			entity.EventType = eventType;
			entity.IdempotencyId = idempotencyId;
			entity.Timestamp = timestamp;
		}

		await context.SaveChangesAsync(cancellationToken);
	}

	static EventStoreEntity CreateEntity(
		string id,
		int entityType,
		string aggregateId,
		string aggregateType,
		int version,
		bool isDeleted,
		string? payload,
		string? eventType,
		string? idempotencyId,
		DateTimeOffset timestamp
	) =>
		new()
		{
			Id = id,
			EntityType = entityType,
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Version = version,
			IsDeleted = isDeleted,
			Payload = payload,
			EventType = eventType,
			IdempotencyId = idempotencyId,
			Timestamp = timestamp,
		};

	static EventStoreEntity ToEntity(RowData row) =>
		new()
		{
			Id = row.Id,
			EntityType = row.EntityType,
			AggregateId = row.AggregateId,
			AggregateType = row.AggregateType,
			Version = row.Version,
			IsDeleted = row.IsDeleted,
			Payload = row.Payload,
			EventType = row.EventType,
			IdempotencyId = row.IdempotencyId,
			Timestamp = row.Timestamp,
		};

	static RowData ToRow(EventStoreEntity entity) =>
		new()
		{
			Id = entity.Id,
			EntityType = entity.EntityType,
			AggregateId = entity.AggregateId,
			AggregateType = entity.AggregateType,
			Version = entity.Version,
			IsDeleted = entity.IsDeleted,
			Payload = entity.Payload,
			EventType = entity.EventType,
			IdempotencyId = entity.IdempotencyId,
			Timestamp = entity.Timestamp,
		};

	bool IsTableEnsured() => EnsuredTables.ContainsKey(_tableEnsureKey);

	void MarkTableEnsured() => EnsuredTables.TryAdd(_tableEnsureKey, 0);

	static async Task CreateStorageTablesWithEfAsync(DbContext context, CancellationToken cancellationToken)
	{
		var creator = context.GetService<IRelationalDatabaseCreator>();
		if (!await creator.ExistsAsync(cancellationToken))
			await creator.CreateAsync(cancellationToken);

		await creator.CreateTablesAsync(cancellationToken);
	}

	static bool IsDuplicateTableCreateError(Exception exception, string _)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current.Message.Contains("already an object named", StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	static void ValidateIdentifier(string identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier))
			throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));

		if (!IdentifierRegex().IsMatch(identifier))
			throw new ArgumentException($"Identifier '{identifier}' contains invalid characters.", nameof(identifier));
	}

	[GeneratedRegex(@"^[\w\-\.]+$")]
	private static partial Regex IdentifierRegex();

	internal sealed class RowData
	{
		public string Id { get; set; } = default!;
		public int EntityType { get; set; }
		public string AggregateId { get; set; } = default!;
		public string AggregateType { get; set; } = default!;
		public int Version { get; set; }
		public bool IsDeleted { get; set; }
		public string? Payload { get; set; }
		public string? EventType { get; set; }
		public string? IdempotencyId { get; set; }
		public DateTimeOffset Timestamp { get; set; }
	}
}
