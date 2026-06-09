# Purview.EventSourcing.SqlServer.Events

`Purview.EventSourcing.SqlServer.Events` adds Azure SQL and SQL Server event stream persistence to Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.SqlServer.Events
```

## Register the provider

```csharp
builder.Services.AddSqlServerEventStore();
```

```json
{
  "ConnectionStrings": {
    "eventstore-sqlserver": "Server=.;Database=MyApp;Trusted_Connection=True;"
  }
}
```

## What it provides

- Event stream persistence for aggregates loaded through `IEventStore`
- SQL Server and Azure SQL configuration binding
- Telemetry registration for the SQL-backed event store

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/sql-server.md

# Purview.EventSourcing.SqlServer.Snapshot

`Purview.EventSourcing.SqlServer.Snapshot` adds Azure SQL and SQL Server queryable snapshot persistence to Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.SqlServer.Snapshot
```

## Register the provider

```csharp
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

- Query, list, count, and snapshot-backed read support through `IQueryableEventStore`
- SQL Server and Azure SQL configuration binding
- JSON-column-backed queryable snapshot persistence

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/sql-server.md
