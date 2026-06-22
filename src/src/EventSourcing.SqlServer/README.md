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
- SQL-specific transaction factory (`ISqlServerEventStoreTransactionFactory`) for enlisting additional SQL/EF work in the same commit
- SQL Server and Azure SQL configuration binding for both event and snapshot stores
- Entity Framework-backed schema creation and CRUD paths
- JSON-column-backed event and snapshot payload storage
- Shared-table safety: when aggregate types share a table, event-stream reads and deletes are scoped by both aggregate id and aggregate type
- Tolerant replay for long-lived streams: integration-tested handling for unknown event types and schema-evolved/unappliable historical events

## Payload shape

The snapshot payload is the fully serialized aggregate graph stored in a single JSON column. EF queries run against that JSON payload, so aggregate properties remain transparent to callers.

Supported members are writable primitives, `[Scalar]` value objects, complex objects composed of supported members, and `EventStoreList<T>` / `EventStoreSet<T>` collections of supported primitive or complex members.

Unsupported shapes fail during model creation, including arrays and collection types other than `EventStoreList<T>` / `EventStoreSet<T>` (for example `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `HashSet<T>`, `ImmutableArray<T>`) and unsupported object types such as dictionaries. Read-only and `[JsonIgnore]` members are excluded from the JSON payload.

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/wiki/SQL-Server-Guide.md
  - Includes behavior notes/caveats (`IsDeletedAsync` missing behavior, tolerant replay, principal requirements)
