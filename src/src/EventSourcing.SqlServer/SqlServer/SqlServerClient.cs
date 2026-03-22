using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace Purview.EventSourcing.SqlServer;

// SQL strings are built from validated identifiers at construction time, not from user input.
#pragma warning disable CA2100

sealed partial class SqlServerClient : IDisposable
{
	readonly SqlServerClientOptions _options;
	readonly string _ensureTableSql;
	readonly string _upsertSql;
	readonly string _deleteSql;
	readonly string _querySql;
	readonly string _getByIdSql;

	volatile bool _tableCreated;

	public SqlServerClient(SqlServerClientOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));

		var quotedSchema = QuoteIdentifier(options.SchemaName);
		var quotedTable = QuoteIdentifier(options.TableName);
		var quotedFullName = $"{quotedSchema}.{quotedTable}";
		var compression = options.UseDataCompression ? " WITH (DATA_COMPRESSION = PAGE)" : "";

		_ensureTableSql = $"""
			IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = @TableName AND s.name = @SchemaName)
			BEGIN
				CREATE TABLE {quotedFullName} (
					[Id] NVARCHAR(450) NOT NULL,
					[AggregateType] NVARCHAR(450) NOT NULL,
					[Payload] NVARCHAR(MAX) NOT NULL,
					CONSTRAINT {QuoteIdentifier($"PK_{options.TableName}")} PRIMARY KEY ([Id])
				){compression};

				-- Covers: QueryByAggregateType (returns Payload via INCLUDE, avoids key lookup)
				CREATE NONCLUSTERED INDEX {QuoteIdentifier($"IX_{options.TableName}_AggregateType")}
					ON {quotedFullName} ([AggregateType])
					INCLUDE ([Payload]){compression};
			END
			""";

		_upsertSql = $"""
			MERGE {quotedFullName} AS target
			USING (SELECT @Id AS Id) AS source
			ON target.[Id] = source.[Id]
			WHEN MATCHED THEN
				UPDATE SET [Payload] = @Payload, [AggregateType] = @AggregateType
			WHEN NOT MATCHED THEN
				INSERT ([Id], [AggregateType], [Payload])
				VALUES (@Id, @AggregateType, @Payload);
			""";

		_deleteSql = $"DELETE FROM {quotedFullName} WHERE [Id] = @Id";

		_querySql = $"SELECT [Payload] FROM {quotedFullName} WHERE [AggregateType] = @AggregateType";

		_getByIdSql = $"SELECT [Payload] FROM {quotedFullName} WHERE [Id] = @Id";
	}

	public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
	{
		if (_tableCreated)
			return;

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _ensureTableSql;
		command.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar, 450) { Value = _options.TableName });
		command.Parameters.Add(
			new SqlParameter("@SchemaName", SqlDbType.NVarChar, 450) { Value = _options.SchemaName }
		);

		await command.ExecuteNonQueryAsync(cancellationToken);
		_tableCreated = true;
	}

	public async Task<bool> UpsertAsync<T>(
		T aggregate,
		string id,
		string aggregateType,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await EnsureConfiguredAsync(cancellationToken);

		var json = JsonConvert.SerializeObject(aggregate, JsonHelpers.JsonSerializerSettings);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _upsertSql;
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });
		command.Parameters.Add(new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType });
		command.Parameters.Add(new SqlParameter("@Payload", SqlDbType.NVarChar, -1) { Value = json });

		var result = await command.ExecuteNonQueryAsync(cancellationToken);
		return result > 0;
	}

	public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _deleteSql;
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });

		var result = await command.ExecuteNonQueryAsync(cancellationToken);
		return result > 0;
	}

	public async Task<List<T>> QueryByAggregateTypeAsync<T>(
		string aggregateType,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _querySql;
		command.Parameters.Add(new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType });

		var results = new List<T>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var payload = reader.GetString(0);
			var item = JsonConvert.DeserializeObject<T>(payload, JsonHelpers.JsonSerializerSettings);
			if (item != null)
				results.Add(item);
		}

		return results;
	}

	public async Task<T?> GetByIdAsync<T>(string id, CancellationToken cancellationToken = default)
		where T : class
	{
		await EnsureConfiguredAsync(cancellationToken);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken);

		await using var command = connection.CreateCommand();
		command.CommandText = _getByIdSql;
		command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (await reader.ReadAsync(cancellationToken))
		{
			var payload = reader.GetString(0);
			return JsonConvert.DeserializeObject<T>(payload, JsonHelpers.JsonSerializerSettings);
		}

		return null;
	}

	async Task EnsureConfiguredAsync(CancellationToken cancellationToken)
	{
		if (_options.AutoCreateTable)
			await EnsureTableExistsAsync(cancellationToken);
	}

	SqlConnection CreateConnection() => new(_options.ConnectionString);

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
			throw new ArgumentException($"Identifier '{identifier}' contains invalid characters.", nameof(identifier));
	}

	[GeneratedRegex(@"^[\w\-\.]+$")]
	private static partial Regex IdentifierRegex();

	public void Dispose()
	{
		// No persistent connections to dispose - connections are created per-operation.
	}
}

sealed class SqlServerClientOptions
{
	public required string ConnectionString { get; init; }

	public string TableName { get; init; } = "Snapshots";

	public string SchemaName { get; init; } = "dbo";

	public bool AutoCreateTable { get; init; } = true;

	public bool UseDataCompression { get; init; }
}
