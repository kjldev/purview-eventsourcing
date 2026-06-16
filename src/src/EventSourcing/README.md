# Purview.EventSourcing

`Purview.EventSourcing` is the core Purview EventSourcing package. It provides aggregate base types, event metadata, provider-agnostic store facades, transaction coordination, and dependency injection extensions for event-sourced .NET applications.

## Install

```bash
dotnet add package Purview.EventSourcing
```

## What is included

- `AggregateBase` and related aggregate infrastructure
- `IEventStore` and `IQueryableEventStore`
- `IEventStoreTransactionFactory` and transaction result types
- Source generation for aggregate events and command methods
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

- SQL Server (events and snapshots): `Purview.EventSourcing.SqlServer`
- Azure Storage (events with blob-backed snapshots/large payloads): `Purview.EventSourcing.AzureStorage`
- MongoDB (events and snapshots): `Purview.EventSourcing.MongoDB`
- Cosmos DB (snapshot only): `Purview.EventSourcing.CosmosDb`

## Documentation

- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
- SQL Server guide: https://github.com/kjldev/purview-eventsourcing/blob/main/docs/sql-server.md
