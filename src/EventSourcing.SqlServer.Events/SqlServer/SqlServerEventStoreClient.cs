using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Purview.EventSourcing.SqlServer;

// SQL strings are built from validated identifiers at construction time, not from user input.
#pragma warning disable CA2100

sealed partial class SqlServerEventStoreClient : IDisposable
{
	readonly SqlServerEventStoreOptions _options;
	readonly string _ensureTableSql;
	readonly string _insertSql;
	readonly string _upsertSql;
	readonly string _deleteByIdSql;
	readonly string _deleteByAggregateIdSql;
	readonly string _getByIdSql;
	readonly string _getByAggregateIdAndEntityTypeSql;
	readonly string _getEventRangeSql;
	readonly string _getIdempotencyMarkersByAggregateIdSql;

	volatile bool _tableCreated;

	public SqlServerEventStoreClient(SqlServerEventStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));

		var quotedSchema = QuoteIdentifier(options.SchemaName);
		var quotedTable = QuoteIdentifier(options.TableName);
		var quotedFullName = $"{quotedSchema}.{quotedTable}";

		_ensureTableSql = $"""
			IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = @TableName AND s.name = @SchemaName)
			BEGIN
				CREATE TABLE {quotedFullName} (
					[Id] NVARCHAR(450) NOT NULL,
					[EntityType] INT NOT NULL,
					[AggregateId] NVARCHAR(450) NOT NULL,
					[AggregateType] NVARCHAR(450) NOT NULL,
					[Version] INT NOT NULL DEFAULT 0,
					[IsDeleted] BIT NOT NULL DEFAULT 0,
					[Payload] NVARCHAR(MAX) NULL,
					[EventType] NVARCHAR(450) NULL,
					[IdempotencyId] NVARCHAR(450) NULL,
					[Timestamp] DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
					CONSTRAINT {QuoteIdentifier($"PK_{options.TableName}")} PRIMARY KEY ([Id])
				);

				-- Covers: GetByAggregateIdAndEntityType, GetIdempotencyMarkers, DeleteByAggregateId
				CREATE NONCLUSTERED INDEX {QuoteIdentifier(
				$"IX_{options.TableName}_AggregateId_EntityType"
			)}
					ON {quotedFullName} ([AggregateId], [EntityType])
					INCLUDE ([Version], [IsDeleted], [AggregateType], [EventType], [IdempotencyId], [Timestamp]);

				-- Covers: GetEventRange (AggregateId + EntityType=1 + Version range, ORDER BY Version)
				CREATE NONCLUSTERED INDEX {QuoteIdentifier($"IX_{options.TableName}_EventRange")}
					ON {quotedFullName} ([AggregateId], [EntityType], [Version])
					INCLUDE ([Payload], [EventType], [IdempotencyId], [IsDeleted], [AggregateType], [Timestamp])
					WHERE [EntityType] = 1;

				-- Covers: GetAggregateIdsAsync (AggregateType + EntityType + optional IsDeleted filter)
				CREATE NONCLUSTERED INDEX {QuoteIdentifier(
				$"IX_{options.TableName}_AggregateType_EntityType"
			)}
					ON {quotedFullName} ([AggregateType], [EntityType], [IsDeleted])
					INCLUDE ([AggregateId]);
			END
			""";

		_insertSql = $"""
			INSERT INTO {quotedFullName} ([Id], [EntityType], [AggregateId], [AggregateType], [Version], [IsDeleted], [Payload], [EventType], [IdempotencyId], [Timestamp])
			VALUES (@Id, @EntityType, @AggregateId, @AggregateType, @Version, @IsDeleted, @Payload, @EventType, @IdempotencyId, @Timestamp)
			""";

		_upsertSql = $"""
			MERGE {quotedFullName} AS target
			USING (SELECT @Id AS Id) AS source
			ON target.[Id] = source.[Id]
			WHEN MATCHED THEN
				UPDATE SET [EntityType] = @EntityType, [AggregateId] = @AggregateId, [AggregateType] = @AggregateType,
					[Version] = @Version, [IsDeleted] = @IsDeleted, [Payload] = @Payload, [EventType] = @EventType,
					[IdempotencyId] = @IdempotencyId, [Timestamp] = @Timestamp
			WHEN NOT MATCHED THEN
				INSERT ([Id], [EntityType], [AggregateId], [AggregateType], [Version], [IsDeleted], [Payload], [EventType], [IdempotencyId], [Timestamp])
				VALUES (@Id, @EntityType, @AggregateId, @AggregateType, @Version, @IsDeleted, @Payload, @EventType, @IdempotencyId, @Timestamp);
			""";

		_deleteByIdSql = $"DELETE FROM {quotedFullName} WHERE [Id] = @Id";

		_deleteByAggregateIdSql =
			$"DELETE FROM {quotedFullName} WHERE [AggregateId] = @AggregateId";

		_getByIdSql =
			$"SELECT [Id], [EntityType], [AggregateId], [AggregateType], [Version], [IsDeleted], [Payload], [EventType], [IdempotencyId], [Timestamp] FROM {quotedFullName} WHERE [Id] = @Id";

		_getByAggregateIdAndEntityTypeSql =
			$"SELECT [Id], [EntityType], [AggregateId], [AggregateType], [Version], [IsDeleted], [Payload], [EventType], [IdempotencyId], [Timestamp] FROM {quotedFullName} WHERE [AggregateId] = @AggregateId AND [EntityType] = @EntityType";

		_getEventRangeSql =
			$"SELECT [Id], [EntityType], [AggregateId], [AggregateType], [Version], [IsDeleted], [Payload], [EventType], [IdempotencyId], [Timestamp] FROM {quotedFullName} WHERE [AggregateId] = @AggregateId AND [EntityType] = 1 AND [Version] >= @VersionFrom AND [Version] <= @VersionTo ORDER BY [Version]";

		_getIdempotencyMarkersByAggregateIdSql =
			$"SELECT [Id] FROM {quotedFullName} WHERE [AggregateId] = @AggregateId AND [EntityType] = 2";
	}

	public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
	{
		if (_tableCreated)
			return;

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _ensureTableSql;
		command.Parameters.Add(
			new SqlParameter("@TableName", SqlDbType.NVarChar, 450) { Value = _options.TableName }
		);
		command.Parameters.Add(
			new SqlParameter("@SchemaName", SqlDbType.NVarChar, 450) { Value = _options.SchemaName }
		);

		await command.ExecuteNonQueryAsync(cancellationToken);
		_tableCreated = true;
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

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _insertSql;
		AddRowParameters(
			command,
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
		);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task InsertBatchAsync(
		List<RowData> rows,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var transaction = (SqlTransaction)
			await connection.BeginTransactionAsync(cancellationToken);
		try
		{
			foreach (var row in rows)
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = _insertSql;
				AddRowParameters(
					command,
					row.Id,
					row.EntityType,
					row.AggregateId,
					row.AggregateType,
					row.Version,
					row.IsDeleted,
					row.Payload,
					row.EventType,
					row.IdempotencyId,
					row.Timestamp
				);

				await command.ExecuteNonQueryAsync(cancellationToken);
			}

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);
			throw;
		}
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

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _upsertSql;
		AddRowParameters(
			command,
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
		);

		await command.ExecuteNonQueryAsync(cancellationToken);
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

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var transaction = (SqlTransaction)
			await connection.BeginTransactionAsync(cancellationToken);
		try
		{
			// Upsert the stream version
			await using (var upsertCommand = connection.CreateCommand())
			{
				upsertCommand.Transaction = transaction;
				upsertCommand.CommandText = _upsertSql;
				AddRowParameters(
					upsertCommand,
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
				);
				await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			// Insert additional rows (events, idempotency markers)
			foreach (var row in additionalInserts)
			{
				await using var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = _insertSql;
				AddRowParameters(
					insertCommand,
					row.Id,
					row.EntityType,
					row.AggregateId,
					row.AggregateType,
					row.Version,
					row.IsDeleted,
					row.Payload,
					row.EventType,
					row.IdempotencyId,
					row.Timestamp
				);
				await insertCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			await transaction.CommitAsync(cancellationToken);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken);
			throw;
		}
	}

	public async Task<bool> DeleteByIdAsync(
		string id,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _deleteByIdSql;
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });

		var result = await command.ExecuteNonQueryAsync(cancellationToken);
		return result > 0;
	}

	public async Task<int> DeleteByAggregateIdAsync(
		string aggregateId,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _deleteByAggregateIdSql;
		command.Parameters.Add(
			new SqlParameter("@AggregateId", SqlDbType.NVarChar, 450) { Value = aggregateId }
		);

		return await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<RowData?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _getByIdSql;
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
	}

	public async Task<RowData?> GetByAggregateIdAndEntityTypeAsync(
		string aggregateId,
		int entityType,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _getByAggregateIdAndEntityTypeSql;
		command.Parameters.Add(
			new SqlParameter("@AggregateId", SqlDbType.NVarChar, 450) { Value = aggregateId }
		);
		command.Parameters.Add(
			new SqlParameter("@EntityType", SqlDbType.Int) { Value = entityType }
		);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
	}

	public async IAsyncEnumerable<RowData> GetEventRangeAsync(
		string aggregateId,
		int versionFrom,
		int versionTo,
		[System.Runtime.CompilerServices.EnumeratorCancellation]
			CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _getEventRangeSql;
		command.Parameters.Add(
			new SqlParameter("@AggregateId", SqlDbType.NVarChar, 450) { Value = aggregateId }
		);
		command.Parameters.Add(
			new SqlParameter("@VersionFrom", SqlDbType.Int) { Value = versionFrom }
		);
		command.Parameters.Add(new SqlParameter("@VersionTo", SqlDbType.Int) { Value = versionTo });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
			yield return ReadRow(reader);
	}

	public async Task<List<string>> GetIdempotencyMarkerIdsByAggregateIdAsync(
		string aggregateId,
		CancellationToken cancellationToken = default
	)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _getIdempotencyMarkersByAggregateIdSql;
		command.Parameters.Add(
			new SqlParameter("@AggregateId", SqlDbType.NVarChar, 450) { Value = aggregateId }
		);

		var results = new List<string>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
			results.Add(reader.GetString(0));

		return results;
	}

	async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
	{
		if (_options.AutoCreateTable)
			await EnsureTableExistsAsync(cancellationToken);
	}

	SqlConnection CreateConnection() => new(_options.ConnectionString);

	static void AddRowParameters(
		SqlCommand command,
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
	)
	{
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });
		command.Parameters.Add(
			new SqlParameter("@EntityType", SqlDbType.Int) { Value = entityType }
		);
		command.Parameters.Add(
			new SqlParameter("@AggregateId", SqlDbType.NVarChar, 450) { Value = aggregateId }
		);
		command.Parameters.Add(
			new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType }
		);
		command.Parameters.Add(new SqlParameter("@Version", SqlDbType.Int) { Value = version });
		command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = isDeleted });
		command.Parameters.Add(
			new SqlParameter("@Payload", SqlDbType.NVarChar, -1)
			{
				Value = (object?)payload ?? DBNull.Value,
			}
		);
		command.Parameters.Add(
			new SqlParameter("@EventType", SqlDbType.NVarChar, 450)
			{
				Value = (object?)eventType ?? DBNull.Value,
			}
		);
		command.Parameters.Add(
			new SqlParameter("@IdempotencyId", SqlDbType.NVarChar, 450)
			{
				Value = (object?)idempotencyId ?? DBNull.Value,
			}
		);
		command.Parameters.Add(
			new SqlParameter("@Timestamp", SqlDbType.DateTimeOffset) { Value = timestamp }
		);
	}

	static RowData ReadRow(SqlDataReader reader) =>
		new()
		{
			Id = reader.GetString(0),
			EntityType = reader.GetInt32(1),
			AggregateId = reader.GetString(2),
			AggregateType = reader.GetString(3),
			Version = reader.GetInt32(4),
			IsDeleted = reader.GetBoolean(5),
			Payload = reader.IsDBNull(6) ? null : reader.GetString(6),
			EventType = reader.IsDBNull(7) ? null : reader.GetString(7),
			IdempotencyId = reader.IsDBNull(8) ? null : reader.GetString(8),
			Timestamp = reader.GetDateTimeOffset(9),
		};

	static string QuoteIdentifier(string identifier)
	{
		ValidateIdentifier(identifier);
		return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
	}

	static void ValidateIdentifier(string identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier))
			throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));

		if (!IdentifierRegex().IsMatch(identifier))
			throw new ArgumentException(
				$"Identifier '{identifier}' contains invalid characters.",
				nameof(identifier)
			);
	}

	[GeneratedRegex(@"^[\w\-\.]+$")]
	private static partial Regex IdentifierRegex();

	public void Dispose()
	{
		// No persistent connections to dispose - connections are created per-operation.
	}

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
