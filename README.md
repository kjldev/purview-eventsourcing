# Purview EventSourcing

Purview EventSourcing is a .NET event sourcing framework with provider-agnostic store facades, source-generated aggregates, multi-aggregate transaction coordination, and sample applications that show how to use the framework with SQL Server, Redis, Azure Blob Storage, and Aspire.

## What the repository contains

| Area | Purpose |
| --- | --- |
| `src/src` | Packable framework packages and shared infrastructure |
| `src/samples/EventSourcing.Samples` | Sample domain and application services |
| `src/samples/EventSourcing.Samples.Web` | Razor Pages sample that uses the non-generic store facades |
| `src/samples/EventSourcing.Samples.AppHost` | Aspire host for the sample environment |
| `src/samples/EventSourcing.Samples.ServiceDefaults` | OpenTelemetry, health check, and service discovery defaults for the sample |
| `src/tests` | Unit, integration, source generator, and sample-focused test projects |
| `docs/sql-server.md` | SQL Server and Azure SQL setup and provider guidance |
| `Justfile` | Local build, test, versioning, packing, and publish workflows |

## Packages

| Package ID | Purpose |
| --- | --- |
| `EventSourcing` | Core abstractions, aggregate base types, facades, transactions, and extensions |
| `Purview.EventSourcing.SourceGenerator` | Source generator for aggregate event classes, registration, and partial command methods |
| `EventSourcing.SqlServer.Events` | SQL Server / Azure SQL event stream store |
| `EventSourcing.SqlServer.Snapshot` | SQL Server / Azure SQL queryable snapshot store |
| `EventSourcing.AzureStorage` | Azure Table / Blob-backed event store |
| `EventSourcing.MongoDB.Events` | MongoDB event store |
| `EventSourcing.MongoDB.Snapshot` | MongoDB queryable snapshot store |
| `EventSourcing.CosmosDb.Snapshot` | Azure Cosmos DB queryable snapshot store |

## Core concepts

- **Provider-agnostic facades**: use `IEventStore` for create/get/save/delete flows and `IQueryableEventStore` for query/list/count flows.
- **Generated aggregates**: annotate partial aggregate methods and let the source generator create event types and registration boilerplate.
- **Transactions**: coordinate multiple aggregate saves with `IEventStoreTransactionFactory` and `IEventStoreTransaction`.
- **Queryable read models**: pair an event stream store with a snapshot/queryable store when your application needs filtering, paging, or projection-style reads.
- **Provider-native coordination when possible**: `EventStoreTransaction` uses a native transaction boundary when all enlisted stores support the same one, and otherwise falls back to shared-correlation logical coordination.

## Quick start

### 1. Define an aggregate

```csharp
using Purview.EventSourcing.Aggregates;

[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    public string CustomerId { get; private set; } = default!;
    public decimal Total { get; private set; }

    [GenerateAggregateEvent]
    public partial void CreateOrder(string customerId);

    [GenerateAggregateEvent]
    public partial void AddLineItem(string productId, string productName, int quantity, decimal unitPrice);
}
```

### 2. Register a store

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

### 3. Use the non-generic facades

```csharp
public sealed class OrderService(IEventStore store)
{
    public async Task PlaceOrderAsync(string orderId, string customerId, CancellationToken cancellationToken)
    {
        var order = await store.GetAsync<OrderAggregate>(orderId, cancellationToken)
            ?? await store.CreateAsync<OrderAggregate>(orderId, cancellationToken: cancellationToken);

        order.CreateOrder(customerId);
        order.AddLineItem("SKU-1", "Demo product", 1, 19.99m);

        await store.SaveAsync(order, cancellationToken);
    }
}
```

### 4. Query through the queryable facade

```csharp
public sealed class OrderQueries(IQueryableEventStore store)
{
    public Task<long> CountActiveOrdersAsync(CancellationToken cancellationToken) =>
        store.CountAsync<OrderAggregate>(o => !o.Details.IsDeleted, cancellationToken);
}
```

### 5. Coordinate a transaction

```csharp
public sealed class CheckoutService(
    IEventStoreTransactionFactory transactionFactory,
    IQueryableEventStore store)
{
    public async Task<bool> CheckoutAsync(
        OrderAggregate order,
        InventoryAggregate inventory,
        CancellationToken cancellationToken)
    {
        await using var transaction = transactionFactory.Create();
        transaction.Enlist(order, store);
        transaction.Enlist(inventory, store);

        var result = await transaction.CommitAsync(cancellationToken);
        return result.Success;
    }
}
```

## Sample application

The sample is a small order, customer, and inventory workflow application that demonstrates how the framework is intended to be consumed:

- `EventSourcing.Samples.Web` uses **non-generic** `IEventStore` / `IQueryableEventStore` facades with the extension methods on those interfaces.
- Multi-aggregate workflows such as checkout, order fulfilment, and stock transfer use `IEventStoreTransactionFactory`.
- The sample registers:
  - `AddSqlServerEventStore()`
  - `AddSqlServerSnapshotQueryableEventStore()`
  - Redis distributed cache when available
  - Azure Blob Storage-backed product images when configured
- `EventSourcing.Samples.AppHost` wires up SQL Server, Redis, Azurite, and the web app for Aspire-driven local runs.

### Sample services worth reading

| Service | What it demonstrates |
| --- | --- |
| `CartCheckoutService` | Multi-item checkout with transactional order + inventory reservation |
| `OrderFulfillmentService` | Order placement + stock reservation through a single transaction |
| `StockTransferService` | Multi-aggregate stock movement between locations |
| `SeedDataService` | Non-generic create, list, count, and save usage across the sample domain |

## Storage providers

| Provider | Registration API | Notes |
| --- | --- | --- |
| SQL Server events | `AddSqlServerEventStore()` | Event stream persistence |
| SQL Server queryable snapshots | `AddSqlServerSnapshotQueryableEventStore()` | Query/list/count support backed by snapshots |
| Azure Table / Blob | `AddAzureTableEventStore()` | Table events with Blob support for large payloads and snapshots |
| MongoDB events | `AddMongoDBEventStore()` | Event stream persistence |
| MongoDB queryable snapshots | `AddMongoDBSnapshotQueryableEventStore()` | Queryable snapshot store |
| Cosmos DB queryable snapshots | `AddCosmosDbQueryableEventStore()` | Queryable snapshot store |

For SQL Server and Azure SQL configuration, permissions, schema routing, and event versioning guidance, see [docs/sql-server.md](docs/sql-server.md).

## Transactions

`EventStoreTransaction` coordinates multiple aggregate saves under a shared correlation ID.

- Enlist aggregates against either the non-generic facade or the typed core store.
- If all enlisted stores share the same native transaction boundary, the framework uses that native coordinator.
- If they do not, the framework falls back to sequential saves under a shared correlation ID.
- On logical fallback, already-saved aggregates are not rolled back; `TransactionResult` reports what succeeded, skipped, or failed.

This lets application code keep a stable API:

```csharp
await using var transaction = transactionFactory.Create();
transaction.Enlist(order, store);
transaction.Enlist(inventory, store);

var result = await transaction.CommitAsync(cancellationToken);
if (!result.Success)
{
    // inspect result.Results for per-aggregate outcomes
}
```

## Development workflow

The repository uses [`just`](https://github.com/casey/just) to wrap the common local workflows.

```powershell
just tools
just restore
just build
just test
just check
just pack
just version
just version-next
just version-bump
```

### Notes

- `just test` runs the executable test projects individually; do **not** rely on solution-level `dotnet test` here because `src/tests/SharedTestingFramework` is a helper library, not a runnable test project.
- Integration tests use Testcontainers and generally require Docker.
- `just pack` builds the packable packages into `artifacts/packages`.

## Testing structure

| Project | Purpose |
| --- | --- |
| `EventSourcing.UnitTests` | Core framework unit tests |
| `EventSourcing.IntegrationTests` | Provider-backed integration tests |
| `EventSourcing.SourceGenerator.UnitTests` | Source generator behavior |
| `EventSourcing.Samples.UnitTests` | Sample domain and service behavior |
| `EventSourcing.Samples.IntegrationTests` | Sample aggregate integration scenarios |
| `EventSourcing.Samples.Web.IntegrationTests` | End-to-end sample web behavior |

## Documentation

- [SQL Server event store guide](docs/sql-server.md)
- Repository wiki content is being prepared to cover getting started, transactions, providers, the sample app, and development workflows.
