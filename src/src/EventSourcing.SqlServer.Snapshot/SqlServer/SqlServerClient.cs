using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Purview.EventSourcing.SqlServer;

// SQL strings are built from validated identifiers at construction time, not from user input.
#pragma warning disable CA2100

sealed partial class SqlServerClient
{
    const int MinimumSqlServerMajorVersion = 16; // SQL Server 2022
    const string MinimumSqlServerVersionName = "SQL Server 2022";
    const int EnsureTableLockTimeoutMilliseconds = 30_000;

    readonly SqlServerClientOptions _options;
    readonly string _ensureTableSql;
    readonly string _ensureTableLockResource;
    readonly string _upsertSql;
    readonly string _deleteSql;
    readonly string _querySql;
    readonly string _countByAggregateTypeSql;
    readonly string _getByIdSql;

    volatile bool _tableCreated;

    public SqlServerClient(SqlServerClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ensureTableLockResource = CreateEnsureTableLockResource(
            options.SchemaName,
            options.TableName
        );

        var quotedSchema = QuoteIdentifier(options.SchemaName);
        var quotedTable = QuoteIdentifier(options.TableName);
        var quotedFullName = $"{quotedSchema}.{quotedTable}";
        var compression = options.UseDataCompression ? " WITH (DATA_COMPRESSION = PAGE)" : "";

        _ensureTableSql = $"""
            DECLARE @LockResult INT;

            BEGIN TRANSACTION;
            BEGIN TRY
            	EXEC @LockResult = sp_getapplock
            		@Resource = @LockResource,
            		@LockMode = 'Exclusive',
            		@LockOwner = 'Transaction',
            		@LockTimeout = @LockTimeoutMs;

            	IF @LockResult < 0
            		THROW 50000, 'Failed to acquire the table creation lock.', 1;

            	IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = @TableName AND s.name = @SchemaName)
            	BEGIN
            		CREATE TABLE {quotedFullName} (
            			[Id] NVARCHAR(450) NOT NULL,
            			[AggregateType] NVARCHAR(450) NOT NULL,
            			[Payload] json NOT NULL,
            			CONSTRAINT {QuoteIdentifier($"PK_{options.TableName}")} PRIMARY KEY ([Id])
            		){compression};

            		-- Covers: QueryByAggregateType (returns Payload via INCLUDE, avoids key lookup)
            		CREATE NONCLUSTERED INDEX {QuoteIdentifier($"IX_{options.TableName}_AggregateType")}
            			ON {quotedFullName} ([AggregateType])
            			INCLUDE ([Payload]){compression};
            	END

            	COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
            	IF XACT_STATE() <> 0
            		ROLLBACK TRANSACTION;

            	THROW;
            END CATCH
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

        _querySql =
            $"SELECT [Payload] FROM {quotedFullName} WHERE [AggregateType] = @AggregateType";

        _countByAggregateTypeSql =
            $"SELECT COUNT_BIG(*) FROM {quotedFullName} WHERE [AggregateType] = @AggregateType";

        _getByIdSql = $"SELECT [Payload] FROM {quotedFullName} WHERE [Id] = @Id";
    }

    public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_tableCreated)
            return;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await CheckServerVersionAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = _ensureTableSql;
        AddEnsureTableParameters(command);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _tableCreated = true;
    }

    public async Task EnsureTableExistsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (_tableCreated)
            return;

        await CheckServerVersionAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = _ensureTableSql;
        AddEnsureTableParameters(command);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _tableCreated = true;
    }

    void AddEnsureTableParameters(SqlCommand command)
    {
        command.Parameters.Add(
            new SqlParameter("@TableName", SqlDbType.NVarChar, 450) { Value = _options.TableName }
        );
        command.Parameters.Add(
            new SqlParameter("@SchemaName", SqlDbType.NVarChar, 450) { Value = _options.SchemaName }
        );
        command.Parameters.Add(
            new SqlParameter("@LockResource", SqlDbType.NVarChar, 255)
            {
                Value = _ensureTableLockResource,
            }
        );
        command.Parameters.Add(
            new SqlParameter("@LockTimeoutMs", SqlDbType.Int)
            {
                Value = EnsureTableLockTimeoutMilliseconds,
            }
        );
    }

    static async Task CheckServerVersionAsync(
        SqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS INT)";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var majorVersion = result is int v ? v : 0;

        if (majorVersion < MinimumSqlServerMajorVersion)
            throw new InvalidOperationException(
                $"SQL Server {MinimumSqlServerVersionName} ({MinimumSqlServerMajorVersion}.x) or later is required "
                    + $"for native JSON column support. The connected server reports major version {majorVersion}."
            );
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

        var json = EventStoreSerializationHelpers.Serialize(aggregate, aggregate!.GetType());

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = _upsertSql;
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });
        command.Parameters.Add(
            new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType }
        );
        command.Parameters.Add(
            new SqlParameter("@Payload", SqlDbType.NVarChar, -1) { Value = json }
        );

        var result = await command.ExecuteNonQueryAsync(cancellationToken);
        return result > 0;
    }

    public async Task<bool> UpsertAsync<T>(
        T aggregate,
        string id,
        string aggregateType,
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var json = EventStoreSerializationHelpers.Serialize(aggregate, aggregate!.GetType());

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = _upsertSql;
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.NVarChar, 450) { Value = id });
        command.Parameters.Add(
            new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType }
        );
        command.Parameters.Add(
            new SqlParameter("@Payload", SqlDbType.NVarChar, -1) { Value = json }
        );

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

    public async Task<long> CountByAggregateTypeAsync(
        string aggregateType,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureConfiguredAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = _countByAggregateTypeSql;
        command.Parameters.Add(
            new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType }
        );

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
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
        command.Parameters.Add(
            new SqlParameter("@AggregateType", SqlDbType.NVarChar, 450) { Value = aggregateType }
        );

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = reader.GetString(0);
            var item = EventStoreSerializationHelpers.Deserialize<T>(payload);
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
            return EventStoreSerializationHelpers.Deserialize<T>(payload);
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

    static string CreateEnsureTableLockResource(string schemaName, string tableName)
    {
        var fullName = $"{schemaName}.{tableName}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullName)));
        return $"Purview.EventSourcing.SqlServer:{hash}";
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
}

sealed class SqlServerClientOptions
{
    public required string ConnectionString { get; init; }

    public string TableName { get; init; } = "Snapshots";

    public string SchemaName { get; init; } = "dbo";

    public bool AutoCreateTable { get; init; } = true;

    public bool UseDataCompression { get; init; }
}
