# Purview.EventSourcing

`Purview.EventSourcing` is the core Purview EventSourcing package. It provides aggregate base types, event metadata, provider-agnostic store facades, transaction coordination, and dependency injection extensions for event-sourced .NET applications.

## Install

```bash
dotnet add package Purview.EventSourcing
```

Add `Purview.EventSourcing.SourceGenerator` when you want generated aggregate event types and command methods.

## What is included

- `AggregateBase` and related aggregate infrastructure
- `IEventStore` and `IQueryableEventStore`
- `IEventStoreTransactionFactory` and transaction result types
- Common extension points used by provider packages
- Dependency injection helpers for core framework services

## Typical usage

```csharp
builder.Services.AddNullQueryableEventStore();
```

```csharp
var order = await store.GetAsync<OrderAggregate>(orderId, cancellationToken)
    ?? await store.CreateAsync<OrderAggregate>(orderId, cancellationToken: cancellationToken);

order.CreateOrder(customerId);
await store.SaveAsync(order, cancellationToken);
```

## Related packages

- Source generator: `Purview.EventSourcing.SourceGenerator`
- SQL Server events: `Purview.EventSourcing.SqlServer.Events`
- SQL Server snapshots: `Purview.EventSourcing.SqlServer.Snapshot`
- Azure Storage events: `Purview.EventSourcing.AzureStorage`
- MongoDB events: `Purview.EventSourcing.MongoDB.Events`
- MongoDB snapshots: `Purview.EventSourcing.MongoDB.Snapshot`
- Cosmos DB snapshots: `Purview.EventSourcing.CosmosDb.Snapshot`

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/sql-server.md
