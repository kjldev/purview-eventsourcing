# Purview.EventSourcing.MongoDB

`Purview.EventSourcing.MongoDB` provides both MongoDB event-stream persistence and MongoDB queryable snapshot persistence for Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.MongoDB
```

## Register the providers

```csharp
builder.Services.AddMongoDBEventStore();
builder.Services.AddMongoDBSnapshotQueryableEventStore();
```

## What it provides

- MongoDB-backed event-stream persistence through `IEventStore`
- Query/list/count/first/single snapshot-backed reads through `IQueryableEventStore`
- Configuration binding for event and snapshot MongoDB stores
- Telemetry registration for MongoDB event and snapshot operations

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
