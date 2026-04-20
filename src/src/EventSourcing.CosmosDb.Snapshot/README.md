# Purview.EventSourcing.CosmosDb.Snapshot

`Purview.EventSourcing.CosmosDb.Snapshot` adds Azure Cosmos DB queryable snapshot persistence to Purview EventSourcing.

## Install

```bash
dotnet add package Purview.EventSourcing.CosmosDb.Snapshot
```

## Register the provider

```csharp
builder.Services.AddCosmosDbQueryableEventStore();
```

## What it provides

- Query, list, count, and snapshot-backed reads through `IQueryableEventStore`
- Azure Cosmos DB persistence for aggregate read models
- Configuration binding for the Cosmos DB snapshot store

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
