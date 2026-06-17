using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.SqlServer.Client;

sealed partial class SqlServerClient
{
	static readonly ConcurrentDictionary<string, SemaphoreSlim> EnsureTableLocks = new(StringComparer.Ordinal);
	static readonly ConcurrentDictionary<string, byte> EnsuredTables = new(StringComparer.Ordinal);

	readonly SqlServerClientOptions _options;
	readonly string _tableEnsureKey;

	public SqlServerClient(SqlServerClientOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_tableEnsureKey = $"{_options.ConnectionString}|{_options.SchemaName}|{_options.TableName}";

		ValidateIdentifier(_options.SchemaName);
		ValidateIdentifier(_options.TableName);
	}

	public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
	{
		if (!_options.AutoCreateTable)
			return;

		await EnsureTableIfEnabledAsync(cancellationToken);
	}

	public async Task EnsureTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(connection);

		if (!_options.AutoCreateTable)
			return;

		await EnsureTableIfEnabledAsync(connection, transaction: null, cancellationToken);
	}

	public async Task<bool> UpsertAsync<T>(
		T aggregate,
		string id,
		string aggregateType,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		await EnsureTableIfEnabledAsync(cancellationToken);
		await using var queryContext = CreateQueryContext<T>();
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

		await EnsureTableIfEnabledAsync(connection, transaction, cancellationToken);

		await using var queryContext = CreateQueryContext<T>(connection);
		await queryContext.Database.UseTransactionAsync(transaction, cancellationToken);
		return await UpsertAsync(aggregate, id, aggregateType, queryContext, cancellationToken);
	}

	public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureTableIfEnabledAsync(cancellationToken);
		await using var context = CreateStorageContext();
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
		await EnsureTableIfEnabledAsync(cancellationToken);
		await using var context = CreateStorageContext();
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
		await EnsureTableIfEnabledAsync(cancellationToken);
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
		await EnsureTableIfEnabledAsync(cancellationToken);
		var normalizedWhereClause = whereClause is null
			? null
			: RewriteAggregateTypePredicate(whereClause, aggregateType);

		var rowQuery = context.Snapshots.AsNoTracking().AsSplitQuery().Where(s => s.AggregateType == aggregateType);

		var aggregateQuery = rowQuery.OrderBy(s => s.Id).Select(s => s.Payload);
		if (normalizedWhereClause != null)
			aggregateQuery = aggregateQuery.Where(normalizedWhereClause);

		if (orderByClause != null)
		{
			aggregateQuery = orderByClause(aggregateQuery);
			EnsureSupportedOrderByExpression(aggregateQuery.Expression);
		}

		var page = aggregateQuery.Skip(Math.Max(0, skipCount)).Take(Math.Max(0, takeCount));
		return await page.ToListAsync(cancellationToken);
	}

	public async Task<T?> GetByIdAsync<T>(string id, CancellationToken cancellationToken = default)
		where T : class
	{
		await using var context = CreateQueryContext<T>();
		await EnsureTableIfEnabledAsync(cancellationToken);
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
	private static partial System.Text.RegularExpressions.Regex IdentifierRegex();

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

		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
		{
			RegisterScalarValueObjectConversions(configurationBuilder, typeof(TAggregate));
		}

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
			var entity = modelBuilder.Entity<SnapshotQueryRow<TAggregate>>();
			entity.ToTable(_options.TableName, _options.SchemaName);
			entity.HasKey(static x => x.Id);
			entity.Property(static x => x.Id).HasMaxLength(450);
			entity.Property(static x => x.AggregateType).HasMaxLength(450);
			entity.HasIndex(static x => x.AggregateType);
			ValidateAggregatePayloadShape(typeof(TAggregate));
			entity.ComplexProperty(
				static x => x.Payload,
				static payload =>
				{
					payload.ToJson();
					ConfigureComplexGraph(payload, typeof(TAggregate));
				}
			);
		}
	}

	static void RegisterScalarValueObjectConversions(ModelConfigurationBuilder configurationBuilder, Type rootType)
	{
		var visited = new HashSet<Type>();
		RegisterScalarValueObjectConversionsRecursive(configurationBuilder, rootType, visited);
	}

	static void RegisterScalarValueObjectConversionsRecursive(
		ModelConfigurationBuilder configurationBuilder,
		Type type,
		HashSet<Type> visited
	)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (!visited.Add(type))
			return;

		var scalarAttribute = type.GetCustomAttribute<ScalarAttribute>();
		if (scalarAttribute is not null)
		{
			RegisterScalarValueObjectConversion(configurationBuilder, type, scalarAttribute);
			return;
		}

		if (!ShouldInspectType(type))
			return;

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (propertyType == typeof(string) || propertyType == typeof(byte[]))
				continue;

			if (TryGetEnumerableElementType(propertyType, out var elementType))
			{
				RegisterScalarValueObjectConversionsRecursive(configurationBuilder, elementType, visited);
				continue;
			}

			RegisterScalarValueObjectConversionsRecursive(configurationBuilder, propertyType, visited);
		}
	}

	static void RegisterScalarValueObjectConversion(
		ModelConfigurationBuilder configurationBuilder,
		Type scalarType,
		ScalarAttribute scalarAttribute
	)
	{
		var scalarProperty =
			scalarType.GetProperty(scalarAttribute.PropertyName, BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException(
				$"'{scalarType.Name}' missing scalar property '{scalarAttribute.PropertyName}'."
			);

		var converterType = typeof(ScalarValueConverter<,>).MakeGenericType(scalarType, scalarProperty.PropertyType);
		configurationBuilder.Properties(scalarType, builder => builder.HaveConversion(converterType));
	}

	static void ConfigureComplexGraph(ComplexPropertyBuilder builder, Type type)
	{
		var visited = new HashSet<Type>();
		ConfigureComplexGraphRecursive(builder, type, visited);
	}

	static void ConfigureComplexGraphRecursive(ComplexPropertyBuilder builder, Type type, HashSet<Type> visited)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (!ShouldInspectType(type) || !visited.Add(type))
			return;

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (ShouldSkipJsonProperty(type, property))
				continue;

			var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (propertyType == typeof(string) || propertyType == typeof(byte[]))
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (propertyType.GetCustomAttribute<ScalarAttribute>() is not null)
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (TryGetEnumerableElementType(propertyType, out var elementType))
			{
				if (IsPrimitiveLike(elementType) || elementType.GetCustomAttribute<ScalarAttribute>() is not null)
					builder.PrimitiveCollection(propertyType, property.Name);
				else if (ShouldInspectType(elementType))
					builder.ComplexCollection(
						propertyType,
						property.Name,
						nested => ConfigureComplexGraphRecursive(nested, elementType, visited)
					);
				else
					throw CreateUnsupportedShapeException(type, property);

				continue;
			}

			if (ShouldOwnType(propertyType))
			{
				builder.ComplexProperty(
					propertyType,
					property.Name,
					nested => ConfigureComplexGraphRecursive(nested, propertyType, visited)
				);
				continue;
			}

			if (IsPrimitiveLike(propertyType))
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (!IsPrimitiveLike(propertyType))
				throw CreateUnsupportedShapeException(type, property);
		}
	}

	static void ConfigureComplexGraphRecursive(ComplexCollectionBuilder builder, Type type, HashSet<Type> visited)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (!ShouldInspectType(type) || !visited.Add(type))
			return;

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (ShouldSkipJsonProperty(type, property))
				continue;

			var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (propertyType == typeof(string) || propertyType == typeof(byte[]))
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (propertyType.GetCustomAttribute<ScalarAttribute>() is not null)
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (TryGetEnumerableElementType(propertyType, out var elementType))
			{
				if (IsPrimitiveLike(elementType) || elementType.GetCustomAttribute<ScalarAttribute>() is not null)
					builder.PrimitiveCollection(propertyType, property.Name);
				else if (ShouldInspectType(elementType))
					builder.ComplexCollection(
						propertyType,
						property.Name,
						nested => ConfigureComplexGraphRecursive(nested, elementType, visited)
					);
				else
					throw CreateUnsupportedShapeException(type, property);

				continue;
			}

			if (ShouldOwnType(propertyType))
			{
				builder.ComplexProperty(
					propertyType,
					property.Name,
					nested => ConfigureComplexGraphRecursive(nested, propertyType, visited)
				);
				continue;
			}

			if (IsPrimitiveLike(propertyType))
			{
				MapReadOnlyConstructorBoundProperty(builder, type, property, propertyType);
				continue;
			}

			if (!IsPrimitiveLike(propertyType))
				throw CreateUnsupportedShapeException(type, property);
		}
	}

	static void MapReadOnlyConstructorBoundProperty(
		ComplexPropertyBuilder builder,
		Type containingType,
		PropertyInfo property,
		Type propertyType
	)
	{
		if (property.GetSetMethod(true) is null && HasBindableConstructorParameter(containingType, property))
			builder.Property(propertyType, property.Name);
	}

	static void MapReadOnlyConstructorBoundProperty(
		ComplexCollectionBuilder builder,
		Type containingType,
		PropertyInfo property,
		Type propertyType
	)
	{
		if (property.GetSetMethod(true) is null && HasBindableConstructorParameter(containingType, property))
			builder.Property(propertyType, property.Name);
	}

	static void ValidateAggregatePayloadShape(Type type)
	{
		var visited = new HashSet<Type>();
		ValidateAggregatePayloadShapeRecursive(type, visited);
	}

	static void ValidateAggregatePayloadShapeRecursive(Type type, HashSet<Type> visited)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (!ShouldInspectType(type) || !visited.Add(type))
			return;

		foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
		{
			if (ShouldSkipJsonProperty(type, property))
				continue;

			var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (propertyType == typeof(string) || propertyType == typeof(byte[]))
				continue;

			if (propertyType.GetCustomAttribute<ScalarAttribute>() is not null)
				continue;

			if (TryGetEnumerableElementType(propertyType, out var elementType))
			{
				if (!IsPrimitiveLike(elementType) && elementType.GetCustomAttribute<ScalarAttribute>() is null)
				{
					if (!ShouldInspectType(elementType))
						throw CreateUnsupportedShapeException(type, property);

					ValidateAggregatePayloadShapeRecursive(elementType, visited);
				}

				continue;
			}

			if (ShouldOwnType(propertyType))
			{
				ValidateAggregatePayloadShapeRecursive(propertyType, visited);
				continue;
			}

			if (!IsPrimitiveLike(propertyType))
				throw CreateUnsupportedShapeException(type, property);
		}
	}

	static bool ShouldOwnType(Type type) =>
		!type.IsPrimitive
		&& !type.IsEnum
		&& type != typeof(string)
		&& type != typeof(decimal)
		&& type != typeof(Guid)
		&& type != typeof(DateTime)
		&& type != typeof(DateTimeOffset)
		&& type != typeof(DateOnly)
		&& type != typeof(TimeOnly)
		&& type != typeof(TimeSpan)
		&& (type.Namespace is null || !type.Namespace.StartsWith("System", StringComparison.Ordinal))
		&& (type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum));

	static bool ShouldInspectType(Type type) =>
		!type.IsPrimitive
		&& !type.IsEnum
		&& type != typeof(string)
		&& type != typeof(decimal)
		&& type != typeof(Guid)
		&& type != typeof(DateTime)
		&& type != typeof(DateTimeOffset)
		&& type != typeof(DateOnly)
		&& type != typeof(TimeOnly)
		&& type != typeof(TimeSpan)
		&& (type.Namespace is null || !type.Namespace.StartsWith("System", StringComparison.Ordinal));

	static bool ShouldSkipJsonProperty(Type containingType, PropertyInfo property) =>
		property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is not null
		|| property.GetGetMethod(true) is null
		|| (property.GetSetMethod(true) is null && !HasBindableConstructorParameter(containingType, property));

	static bool HasBindableConstructorParameter(Type containingType, PropertyInfo property)
	{
		var constructors = containingType.GetConstructors(
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
		);
		foreach (var constructor in constructors)
		{
			foreach (var parameter in constructor.GetParameters())
			{
				if (
					string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase)
					&& parameter.ParameterType == property.PropertyType
				)
					return true;
			}
		}

		return false;
	}

	static bool IsPrimitiveLike(Type type) =>
		type.IsPrimitive
		|| type.IsEnum
		|| type == typeof(string)
		|| type == typeof(decimal)
		|| type == typeof(Guid)
		|| type == typeof(DateTime)
		|| type == typeof(DateTimeOffset)
		|| type == typeof(DateOnly)
		|| type == typeof(TimeOnly)
		|| type == typeof(TimeSpan)
		|| type == typeof(byte[]);

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
		)
		{
			elementType = type.GetGenericArguments()[0];
			return true;
		}

		return false;
	}

	static InvalidOperationException CreateUnsupportedShapeException(Type containingType, MemberInfo member) =>
		new(
			$"{containingType.Name}.{member.Name} cannot be mapped into the snapshot JSON payload. "
				+ "Supported members are primitive types, [Scalar] value objects, complex types, and arrays/collections of those shapes."
		);

	static void EnsureSupportedOrderByExpression(Expression queryExpression) =>
		new UnsupportedScalarValueOrderByDetector().Visit(queryExpression);

	static Expression<Func<T, bool>> RewriteAggregateTypePredicate<T>(
		Expression<Func<T, bool>> whereClause,
		string aggregateType
	)
		where T : class
	{
		var aggregateTypeVisitor = new AggregateTypeExpressionVisitor(aggregateType);
		var rewritten = (Expression<Func<T, bool>>)aggregateTypeVisitor.Visit(whereClause)!;
		var invariantCasingVisitor = new InvariantStringMethodNormalizationVisitor();
		rewritten = (Expression<Func<T, bool>>)invariantCasingVisitor.Visit(rewritten)!;
		var scalarVisitor = new ScalarValueMemberAccessPredicateVisitor();
		return (Expression<Func<T, bool>>)scalarVisitor.Visit(rewritten)!;
	}

	sealed class AggregateTypeExpressionVisitor(string aggregateType) : ExpressionVisitor
	{
		readonly ConstantExpression _aggregateType = Expression.Constant(aggregateType, typeof(string));

		protected override Expression VisitMember(MemberExpression node) =>
			node.Member.Name == nameof(IAggregate.AggregateType)
			&& node.Expression is not null
			&& typeof(IAggregate).IsAssignableFrom(node.Expression.Type)
				? _aggregateType
				: base.VisitMember(node);
	}

	sealed class UnsupportedScalarValueOrderByDetector : ExpressionVisitor
	{
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (
				node.Method.DeclaringType == typeof(Queryable)
				&& (
					node.Method.Name == nameof(Queryable.OrderBy)
					|| node.Method.Name == nameof(Queryable.OrderByDescending)
					|| node.Method.Name == nameof(Queryable.ThenBy)
					|| node.Method.Name == nameof(Queryable.ThenByDescending)
				)
			)
			{
				var lambda = (LambdaExpression)StripQuotes(node.Arguments[1]);
				new ScalarValueMemberAccessDetector().Visit(lambda.Body);
			}

			return base.VisitMethodCall(node);
		}

		static Expression StripQuotes(Expression expression)
		{
			while (expression.NodeType == ExpressionType.Quote)
				expression = ((UnaryExpression)expression).Operand;

			return expression;
		}
	}

	sealed class ScalarValueMemberAccessPredicateVisitor : ExpressionVisitor
	{
		protected override Expression VisitBinary(BinaryExpression node)
		{
			var visitedLeft = Visit(node.Left);
			var visitedRight = Visit(node.Right);

			if (
				(node.NodeType == ExpressionType.Equal || node.NodeType == ExpressionType.NotEqual)
				&& TryRewriteScalarPrimitiveComparison(
					visitedLeft,
					visitedRight,
					out var rewrittenLeft,
					out var rewrittenRight
				)
			)
			{
				return Expression.MakeBinary(node.NodeType, rewrittenLeft, rewrittenRight);
			}

			return node.Update(visitedLeft, node.Conversion, visitedRight);
		}

		protected override Expression VisitMember(MemberExpression node)
		{
			var visitedExpression = base.VisitMember(node);
			if (visitedExpression is not MemberExpression visited || visited.Expression is null)
				return visitedExpression;

			var scalarAttribute = visited.Expression.Type.GetCustomAttribute<ScalarAttribute>();
			if (scalarAttribute is null || visited.Member.Name != scalarAttribute.PropertyName)
				return visited;

			try
			{
				return Expression.Convert(visited.Expression, visited.Type);
			}
			catch (InvalidOperationException)
			{
				return visited;
			}
		}

		static bool TryRewriteScalarPrimitiveComparison(
			Expression left,
			Expression right,
			out Expression rewrittenLeft,
			out Expression rewrittenRight
		)
		{
			rewrittenLeft = left;
			rewrittenRight = right;

			if (
				TryGetScalarPropertyType(left.Type, out var leftScalarType)
				&& IsSameOrNullable(right.Type, leftScalarType)
			)
			{
				rewrittenLeft = Expression.Convert(left, right.Type);
				return true;
			}

			if (
				TryGetScalarPropertyType(right.Type, out var rightScalarType)
				&& IsSameOrNullable(left.Type, rightScalarType)
			)
			{
				rewrittenRight = Expression.Convert(right, left.Type);
				return true;
			}

			return false;
		}

		static bool TryGetScalarPropertyType(Type candidateType, out Type scalarPropertyType)
		{
			scalarPropertyType = null!;
			var scalarAttribute = candidateType.GetCustomAttribute<ScalarAttribute>();
			if (scalarAttribute is null)
				return false;

			var scalarProperty = candidateType.GetProperty(
				scalarAttribute.PropertyName,
				BindingFlags.Instance | BindingFlags.Public
			);
			if (scalarProperty is null)
				return false;

			scalarPropertyType = scalarProperty.PropertyType;
			return true;
		}

		static bool IsSameOrNullable(Type candidate, Type expected)
		{
			var underlyingType = Nullable.GetUnderlyingType(candidate);
			return candidate == expected || underlyingType == expected;
		}
	}

	sealed class InvariantStringMethodNormalizationVisitor : ExpressionVisitor
	{
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			var visitedObject = Visit(node.Object);
			var visitedArguments = Visit(node.Arguments);

			if (node.Method.DeclaringType == typeof(string) && node.Arguments.Count == 0 && visitedObject is not null)
			{
				if (node.Method.Name == nameof(string.ToLowerInvariant))
					return Expression.Call(visitedObject, nameof(string.ToLower), Type.EmptyTypes);

				if (node.Method.Name == nameof(string.ToUpperInvariant))
					return Expression.Call(visitedObject, nameof(string.ToUpper), Type.EmptyTypes);
			}

			return node.Update(visitedObject, visitedArguments);
		}
	}

	sealed class ScalarValueMemberAccessDetector : ExpressionVisitor
	{
		protected override Expression VisitMember(MemberExpression node)
		{
			var visited = base.VisitMember(node);
			if (node.Expression is null)
				return visited;

			var scalarAttribute = node.Expression.Type.GetCustomAttribute<ScalarAttribute>();
			return scalarAttribute is not null && node.Member.Name == scalarAttribute.PropertyName
				? throw new InvalidOperationException(
					$"Ordering by '{node.Expression.Type.Name}.{node.Member.Name}' is not supported for SQL snapshot JSON queries. "
						+ "Order by the scalar value object property itself (for example: c => c.Name) instead of its inner scalar member."
				)
				: visited;
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
				await using var context = CreateStorageContext();
				await CreateStorageTablesWithEfAsync(context, cancellationToken);
			}
			catch (Exception ex) when (IsDuplicateTableCreateError(ex, _options.TableName))
			{
				// Another writer won the create race for the same table.
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
				await using var context = CreateStorageContext(connection);
				if (transaction is not null)
					await context.Database.UseTransactionAsync(transaction, cancellationToken);
				await CreateStorageTablesWithEfAsync(context, cancellationToken);
			}
			catch (Exception ex) when (IsDuplicateTableCreateError(ex, _options.TableName))
			{
				// Another writer won the create race for the same table.
			}

			MarkTableEnsured();
		}
		finally
		{
			tableLock.Release();
		}
	}

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
}
