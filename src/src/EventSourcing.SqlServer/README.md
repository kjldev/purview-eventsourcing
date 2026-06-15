# Purview.EventSourcing.SqlServer

`Purview.EventSourcing.SqlServer` provides both SQL Server/Azure SQL event-stream persistence and SQL-backed queryable snapshot persistence for Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.SqlServer
```

## Register the providers

```csharp
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();
```

```json
{
  "ConnectionStrings": {
    "eventstore-sqlserver": "Server=.;Database=MyApp;Trusted_Connection=True;"
  }
}
```

## What it provides

- Event-stream persistence for aggregates loaded through `IEventStore`
- Query/list/count snapshot-backed reads through `IQueryableEventStore`
- SQL Server and Azure SQL configuration binding for both event and snapshot stores
- Entity Framework-backed schema creation and CRUD paths
- JSON-column-backed event and snapshot payload storage

## Payload shape

The snapshot payload is the fully serialized aggregate graph stored in a single JSON column. EF queries run against that JSON payload, so aggregate properties remain transparent to callers.

Supported members are writable primitives, `[Scalar]` value objects, complex objects composed of supported members, and arrays/read-write collections of supported primitive or complex members.

Unsupported shapes fail during model creation, including immutable collection types such as `ImmutableArray<T>` and unsupported object types such as dictionaries. Read-only and `[JsonIgnore]` members are excluded from the JSON payload.

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/sql-server.md
