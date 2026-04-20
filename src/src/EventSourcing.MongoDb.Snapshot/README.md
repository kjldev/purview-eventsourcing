# Purview.EventSourcing.MongoDB.Snapshot

`Purview.EventSourcing.MongoDB.Snapshot` adds MongoDB queryable snapshot persistence to Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.MongoDB.Snapshot
```

## Register the provider

```csharp
builder.Services.AddMongoDBSnapshotQueryableEventStore();
```

## What it provides

- Query, list, count, and first/single lookups through `IQueryableEventStore`
- MongoDB-backed snapshot persistence for aggregate read models
- Configuration binding for the MongoDB snapshot store

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
