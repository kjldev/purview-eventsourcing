namespace Purview.EventSourcing.Admin.SQLServer.Internal;

sealed record SqlServerAdminTableDescriptor(string? AggregateTypeFilter, string SchemaName, string TableName);
