using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.SqlServer;

sealed partial class SqlServerClient
{
	readonly SqlServerClientOptions _options;
	readonly string _ensureTableSql;
	readonly string _ensureTableLockResource;
	readonly string _quotedFullName;

	public SqlServerClient(SqlServerClientOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_ensureTableLockResource = CreateEnsureTableLockResource(_options.SchemaName, _options.TableName);

		var quotedSchema = QuoteIdentifier(_options.SchemaName);
		var quotedTable = QuoteIdentifier(_options.TableName);
		_quotedFullName = $"{quotedSchema}.{quotedTable}";

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

				IF OBJECT_ID(@FullTableName, 'U') IS NULL
				BEGIN
					CREATE TABLE {_quotedFullName} (
						[Id] NVARCHAR(450) NOT NULL,
						[AggregateType] NVARCHAR(450) NOT NULL,
						[Payload] json NOT NULL,
						CONSTRAINT {QuoteIdentifier($"PK_{_options.TableName}")} PRIMARY KEY ([Id])
					);

					CREATE NONCLUSTERED INDEX {QuoteIdentifier($"IX_{_options.TableName}_AggregateType")}
						ON {_quotedFullName} ([AggregateType])
						INCLUDE ([Payload]);
				END

				COMMIT TRANSACTION;
			END TRY
			BEGIN CATCH
				IF XACT_STATE() <> 0
					ROLLBACK TRANSACTION;

				THROW;
			END CATCH
			""";

		ValidateIdentifier(_options.SchemaName);
		ValidateIdentifier(_options.TableName);
	}

	public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
	{
		if (!_options.AutoCreateTable)
			return;

		await using var context = CreateStorageContext();
		await EnsureTableWithEfAsync(context, cancellationToken);
	}

	public async Task EnsureTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(connection);

		if (!_options.AutoCreateTable)
			return;

		await using var context = CreateStorageContext(connection);
		await EnsureTableWithEfAsync(context, cancellationToken);
	}

	public async Task<bool> UpsertAsync<T>(
		T aggregate,
		string id,
		string aggregateType,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await using var context = CreateStorageContext();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		await using var queryContext = CreateQueryContext<T>();
		await EnsureTableIfEnabledAsync(queryContext, cancellationToken);
		return await UpsertAsync(aggregate, id, aggregateType, queryContext, cancellationToken);
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

		await using var ensureContext = CreateStorageContext(connection);
		await ensureContext.Database.UseTransactionAsync(transaction, cancellationToken);
		await EnsureTableIfEnabledAsync(ensureContext, cancellationToken);

		await using var queryContext = CreateQueryContext<T>(connection);
		await queryContext.Database.UseTransactionAsync(transaction, cancellationToken);
		return await UpsertAsync(aggregate, id, aggregateType, queryContext, cancellationToken);
	}

	public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		await using var context = CreateStorageContext();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		var existing = await context.Snapshots.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
		if (existing is null)
			return false;

		context.Snapshots.Remove(existing);
		return await context.SaveChangesAsync(cancellationToken) > 0;
	}

	public async Task<long> CountByAggregateTypeAsync(
		string aggregateType,
		CancellationToken cancellationToken = default
	)
	{
		await using var context = CreateStorageContext();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		return await context
			.Snapshots.AsNoTracking()
			.LongCountAsync(s => s.AggregateType == aggregateType, cancellationToken);
	}

	public async Task<long> CountByAggregateTypeAsync<T>(
		string aggregateType,
		Expression<Func<T, bool>>? whereClause,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await using var context = CreateQueryContext<T>();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		var query = BuildAggregateQuery(context, aggregateType, whereClause);
		return await query.LongCountAsync(cancellationToken);
	}

	public async Task<List<T>> QueryByAggregateTypeAsync<T>(
		string aggregateType,
		Expression<Func<T, bool>>? whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int skipCount,
		int takeCount,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await using var context = CreateQueryContext<T>();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		var normalizedWhereClause = whereClause is null
			? null
			: RewriteAggregateTypePredicate(whereClause, aggregateType);

		var rowQuery = context.Snapshots.AsNoTracking().AsSplitQuery().Where(s => s.AggregateType == aggregateType);

		var aggregateQuery = rowQuery.OrderBy(s => s.Id).Select(s => s.Payload);
		if (normalizedWhereClause != null)
			aggregateQuery = aggregateQuery.Where(normalizedWhereClause);

		if (orderByClause != null)
			aggregateQuery = orderByClause(aggregateQuery);

		var page = aggregateQuery.Skip(Math.Max(0, skipCount)).Take(Math.Max(0, takeCount));
		return await page.ToListAsync(cancellationToken);
	}

	public async Task<T?> GetByIdAsync<T>(string id, CancellationToken cancellationToken = default)
		where T : class
	{
		await using var context = CreateQueryContext<T>();
		await EnsureTableIfEnabledAsync(context, cancellationToken);
		var query = context.Snapshots.AsNoTracking().AsSplitQuery().Where(s => s.Id == id).Select(s => s.Payload);
		return await query.SingleOrDefaultAsync(cancellationToken);
	}

	static IQueryable<T> BuildAggregateQuery<T>(
		SnapshotQueryDbContext<T> context,
		string aggregateType,
		Expression<Func<T, bool>>? whereClause
	)
		where T : class
	{
		var query = context
			.Snapshots.AsNoTracking()
			.Where(s => s.AggregateType == aggregateType)
			.Select(s => s.Payload);

		return whereClause is null ? query : query.Where(RewriteAggregateTypePredicate(whereClause, aggregateType));
	}

	static async Task<bool> UpsertAsync<T>(
		T aggregate,
		string id,
		string aggregateType,
		SnapshotQueryDbContext<T> context,
		CancellationToken cancellationToken
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		var exists = await context.Snapshots.AsNoTracking().AnyAsync(s => s.Id == id, cancellationToken);
		var entity = new SnapshotQueryRow<T>
		{
			Id = id,
			AggregateType = aggregateType,
			Payload = aggregate,
		};

		if (exists)
			context.Update(entity);
		else
			context.Add(entity);

		return await context.SaveChangesAsync(cancellationToken) > 0;
	}

	SnapshotStorageDbContext CreateStorageContext() => new(_options);

	SnapshotStorageDbContext CreateStorageContext(SqlConnection connection) => new(_options, connection);

	SnapshotQueryDbContext<T> CreateQueryContext<T>()
		where T : class => new(_options);

	SnapshotQueryDbContext<T> CreateQueryContext<T>(SqlConnection connection)
		where T : class => new(_options, connection);

	static void ValidateIdentifier(string identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier))
			throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));

		if (!IdentifierRegex().IsMatch(identifier))
			throw new ArgumentException($"Identifier '{identifier}' contains invalid characters.", nameof(identifier));
	}

	[GeneratedRegex(@"^[\w\-\.]+$")]
	private static partial Regex IdentifierRegex();

	sealed class SnapshotStorageDbContext : DbContext
	{
		readonly SqlServerClientOptions _options;
		readonly SqlConnection? _connection;

		public SnapshotStorageDbContext(SqlServerClientOptions options)
		{
			_options = options;
		}

		public SnapshotStorageDbContext(SqlServerClientOptions options, SqlConnection connection)
		{
			_options = options;
			_connection = connection;
		}

		public DbSet<SnapshotStorageRow> Snapshots => Set<SnapshotStorageRow>();

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (_connection is null)
				optionsBuilder.UseSqlServer(_options.ConnectionString);
			else
				optionsBuilder.UseSqlServer(_connection);

			optionsBuilder.ReplaceService<IModelCacheKeyFactory, SnapshotModelCacheKeyFactory>();
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			var entity = modelBuilder.Entity<SnapshotStorageRow>();
			entity.ToTable(_options.TableName, _options.SchemaName);
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Id).HasMaxLength(450);
			entity.Property(x => x.AggregateType).HasMaxLength(450);
			entity.Property(x => x.Payload).HasColumnType("json");
			entity.HasIndex(x => x.AggregateType);
		}
	}

	sealed class SnapshotQueryDbContext<TAggregate> : DbContext
		where TAggregate : class
	{
		readonly SqlServerClientOptions _options;
		readonly SqlConnection? _connection;

		public SnapshotQueryDbContext(SqlServerClientOptions options)
		{
			_options = options;
		}

		public SnapshotQueryDbContext(SqlServerClientOptions options, SqlConnection connection)
		{
			_options = options;
			_connection = connection;
		}

		public DbSet<SnapshotQueryRow<TAggregate>> Snapshots => Set<SnapshotQueryRow<TAggregate>>();

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (_connection is null)
				optionsBuilder.UseSqlServer(_options.ConnectionString);
			else
				optionsBuilder.UseSqlServer(_connection);

			optionsBuilder.ReplaceService<IModelCacheKeyFactory, SnapshotModelCacheKeyFactory>();
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			RegisterOwnedTypes(modelBuilder, typeof(TAggregate));

			var entity = modelBuilder.Entity<SnapshotQueryRow<TAggregate>>();
			entity.ToTable(_options.TableName, _options.SchemaName);
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Id).HasMaxLength(450);
			entity.Property(x => x.AggregateType).HasMaxLength(450);
			entity.HasIndex(x => x.AggregateType);
			entity.OwnsOne(
				x => x.Payload,
				payload =>
				{
					payload.ToJson();
				}
			);
		}
	}

	static void RegisterOwnedTypes(ModelBuilder modelBuilder, Type rootType)
	{
		var visited = new HashSet<Type>();
		RegisterOwnedTypesRecursive(modelBuilder, rootType, visited);
	}

	static void RegisterOwnedTypesRecursive(ModelBuilder modelBuilder, Type type, HashSet<Type> visited)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (!ShouldOwnType(type) || !visited.Add(type))
			return;

		modelBuilder.Owned(type);

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (propertyType == typeof(string) || propertyType == typeof(byte[]))
				continue;

			if (TryGetEnumerableElementType(propertyType, out var elementType))
			{
				RegisterOwnedTypesRecursive(modelBuilder, elementType, visited);
				continue;
			}

			RegisterOwnedTypesRecursive(modelBuilder, propertyType, visited);
		}
	}

	static bool ShouldOwnType(Type type)
	{
		if (type.IsPrimitive || type.IsEnum)
			return false;

		if (
			type == typeof(string)
			|| type == typeof(decimal)
			|| type == typeof(Guid)
			|| type == typeof(DateTime)
			|| type == typeof(DateTimeOffset)
			|| type == typeof(DateOnly)
			|| type == typeof(TimeOnly)
			|| type == typeof(TimeSpan)
		)
			return false;

		if (type.Namespace is not null && type.Namespace.StartsWith("System", StringComparison.Ordinal))
			return false;

		return type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
	}

	static bool TryGetEnumerableElementType(Type type, out Type elementType)
	{
		elementType = null!;

		if (type.IsArray)
		{
			elementType = type.GetElementType()!;
			return true;
		}

		if (!type.IsGenericType)
			return false;

		var genericDefinition = type.GetGenericTypeDefinition();
		if (
			genericDefinition == typeof(IEnumerable<>)
			|| genericDefinition == typeof(ICollection<>)
			|| genericDefinition == typeof(IList<>)
			|| genericDefinition == typeof(List<>)
			|| genericDefinition == typeof(IReadOnlyList<>)
			|| genericDefinition == typeof(IReadOnlyCollection<>)
			|| genericDefinition == typeof(HashSet<>)
			|| genericDefinition == typeof(ImmutableArray<>)
		)
		{
			elementType = type.GetGenericArguments()[0];
			return true;
		}

		return false;
	}

	static Expression<Func<T, bool>> RewriteAggregateTypePredicate<T>(
		Expression<Func<T, bool>> whereClause,
		string aggregateType
	)
		where T : class
	{
		var visitor = new AggregateTypeExpressionVisitor(aggregateType);
		return (Expression<Func<T, bool>>)visitor.Visit(whereClause)!;
	}

	sealed class AggregateTypeExpressionVisitor(string aggregateType) : ExpressionVisitor
	{
		readonly ConstantExpression _aggregateType = Expression.Constant(aggregateType, typeof(string));

		protected override Expression VisitMember(MemberExpression node)
		{
			if (
				node.Member.Name == nameof(IAggregate.AggregateType)
				&& node.Expression is not null
				&& typeof(IAggregate).IsAssignableFrom(node.Expression.Type)
			)
				return _aggregateType;

			return base.VisitMember(node);
		}
	}

	sealed class SnapshotModelCacheKeyFactory : IModelCacheKeyFactory
	{
		public object Create(DbContext context, bool designTime)
		{
			var optionsField = context.GetType().GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
			if (optionsField?.GetValue(context) is SqlServerClientOptions options)
			{
				if (
					context.GetType().IsGenericType
					&& context.GetType().GetGenericTypeDefinition() == typeof(SnapshotQueryDbContext<>)
				)
				{
					var aggregateType = context.GetType().GenericTypeArguments[0];
					return (context.GetType(), aggregateType, options.SchemaName, options.TableName, designTime);
				}

				return (context.GetType(), options.SchemaName, options.TableName, designTime);
			}

			return (context.GetType(), designTime);
		}
	}

	async Task EnsureTableIfEnabledAsync(DbContext context, CancellationToken cancellationToken)
	{
		if (_options.AutoCreateTable)
			await EnsureTableWithEfAsync(context, cancellationToken);
	}

	async Task EnsureTableWithEfAsync(DbContext context, CancellationToken cancellationToken)
	{
		var fullTableName = $"{_options.SchemaName}.{_options.TableName}";
		var lockTimeout = 30_000;

		await context.Database.ExecuteSqlRawAsync(
			_ensureTableSql,
			[
				new SqlParameter("@LockResource", _ensureTableLockResource),
				new SqlParameter("@LockTimeoutMs", lockTimeout),
				new SqlParameter("@FullTableName", fullTableName),
			],
			cancellationToken
		);
	}

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
}

sealed class SnapshotStorageRow
{
	public required string Id { get; set; }

	public required string AggregateType { get; set; }

	public required string Payload { get; set; }
}

sealed class SnapshotQueryRow<TAggregate>
	where TAggregate : class
{
	public required string Id { get; set; }

	public required string AggregateType { get; set; }

	public required TAggregate Payload { get; set; }
}

sealed class SqlServerClientOptions
{
	public required string ConnectionString { get; init; }

	public string TableName { get; init; } = "Snapshots";

	public string SchemaName { get; init; } = "dbo";

	public bool AutoCreateTable { get; init; } = true;

	public bool UseDataCompression { get; init; }
}
