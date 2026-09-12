using Purview.EventSourcing.SqlServer.Events;

namespace Purview.EventSourcing.Admin.SQLServer.Internal;

static class SqlServerAdminTableResolver
{
	public static IReadOnlyList<SqlServerAdminTableDescriptor> ResolveTables(
		SqlServerEventStoreOptions options,
		string? aggregateType
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!string.IsNullOrWhiteSpace(aggregateType))
		{
			return [ResolveTable(options, aggregateType)];
		}

		List<SqlServerAdminTableDescriptor> tables = [new(null, options.SchemaName, options.TableName)];

		foreach (var overrideEntry in options.AggregateTableOverrides)
		{
			if (overrideEntry.Value is null)
				continue;

			tables.Add(
				new SqlServerAdminTableDescriptor(
					overrideEntry.Key,
					overrideEntry.Value.SchemaName ?? options.SchemaName,
					overrideEntry.Value.TableName ?? options.TableName
				)
			);
		}

		return tables;
	}

	public static SqlServerAdminTableDescriptor ResolveTable(SqlServerEventStoreOptions options, string aggregateType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		if (!options.AggregateTableOverrides.TryGetValue(aggregateType, out var overrideEntry) || overrideEntry is null)
		{
			return new SqlServerAdminTableDescriptor(aggregateType, options.SchemaName, options.TableName);
		}

		// If an override exists for the aggregate type, use the override values (if provided) or fall back to the default options
		return new SqlServerAdminTableDescriptor(
			aggregateType,
			overrideEntry.SchemaName ?? options.SchemaName,
			overrideEntry.TableName ?? options.TableName
		);
	}
}
