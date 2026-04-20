# Purview EventSourcing

Purview EventSourcing is a .NET event sourcing framework for building aggregate-based applications with provider-agnostic store facades, source-generated aggregates, transaction coordination, and storage packages for SQL Server, Azure Storage, MongoDB, and Azure Cosmos DB.

## Why use it

- Build aggregates on top of `AggregateBase` and load/save them through `IEventStore`.
- Add queryable read models with `IQueryableEventStore` when you need filtering, paging, and list views.
- Generate aggregate event types and registration code from partial methods with the source generator package.
- Coordinate multi-aggregate saves through `IEventStoreTransactionFactory`.
- Swap storage providers without changing your application-facing aggregate APIs.

## Packages

| Package ID | Purpose | Project README |
| --- | --- | --- |
| `Purview.EventSourcing` | Core abstractions, aggregate types, facades, transactions, and DI extensions | [`src/src/EventSourcing/README.md`](src/src/EventSourcing/README.md) |
| `Purview.EventSourcing.Shared` | Shared abstractions and infrastructure used by provider packages | [`src/src/EventSourcing.Shared/README.md`](src/src/EventSourcing.Shared/README.md) |
| `Purview.EventSourcing.SourceGenerator` | Source generator for aggregate event types, registration, and command methods | [`src/src/EventSourcing.SourceGenerator/README.md`](src/src/EventSourcing.SourceGenerator/README.md) |
| `Purview.EventSourcing.SqlServer.Events` | Azure SQL / SQL Server event stream store | [`src/src/EventSourcing.SqlServer.Events/README.md`](src/src/EventSourcing.SqlServer.Events/README.md) |
| `Purview.EventSourcing.SqlServer.Snapshot` | Azure SQL / SQL Server queryable snapshot store | [`src/src/EventSourcing.SqlServer.Snapshot/README.md`](src/src/EventSourcing.SqlServer.Snapshot/README.md) |
| `Purview.EventSourcing.AzureStorage` | Azure Table / Blob event store | [`src/src/EventSourcing.AzureStorage/README.md`](src/src/EventSourcing.AzureStorage/README.md) |
| `Purview.EventSourcing.MongoDB.Events` | MongoDB event stream store | [`src/src/EventSourcing.MongoDb.Events/README.md`](src/src/EventSourcing.MongoDb.Events/README.md) |
| `Purview.EventSourcing.MongoDB.Snapshot` | MongoDB queryable snapshot store | [`src/src/EventSourcing.MongoDb.Snapshot/README.md`](src/src/EventSourcing.MongoDb.Snapshot/README.md) |
| `Purview.EventSourcing.CosmosDb.Snapshot` | Azure Cosmos DB queryable snapshot store | [`src/src/EventSourcing.CosmosDb.Snapshot/README.md`](src/src/EventSourcing.CosmosDb.Snapshot/README.md) |

## Install the packages you need

```bash
dotnet add package Purview.EventSourcing
dotnet add package Purview.EventSourcing.SourceGenerator
dotnet add package Purview.EventSourcing.SqlServer.Events
dotnet add package Purview.EventSourcing.SqlServer.Snapshot
```

Provider packages layer on top of the core `Purview.EventSourcing` package. Add only the packages required for your chosen persistence strategy.

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

### 2. Register storage

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

### 3. Load and save through the provider-agnostic facade

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

### 4. Query through a snapshot-backed facade

```csharp
public sealed class OrderQueries(IQueryableEventStore store)
{
    public Task<long> CountActiveOrdersAsync(CancellationToken cancellationToken) =>
        store.CountAsync<OrderAggregate>(o => !o.Details.IsDeleted, cancellationToken);
}
```

### 5. Coordinate multi-aggregate saves

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

## Storage provider matrix

| Provider | Package | Registration API | Notes |
| --- | --- | --- | --- |
| Core only | `Purview.EventSourcing` | `AddNullQueryableEventStore()` | No persistent query store |
| Azure SQL / SQL Server events | `Purview.EventSourcing.SqlServer.Events` | `AddSqlServerEventStore()` | Event stream persistence |
| Azure SQL / SQL Server snapshots | `Purview.EventSourcing.SqlServer.Snapshot` | `AddSqlServerSnapshotQueryableEventStore()` | Query/list/count backed by snapshots |
| Azure Table / Blob | `Purview.EventSourcing.AzureStorage` | `AddAzureTableEventStore()` | Table events plus Blob support for large payloads and snapshots |
| MongoDB events | `Purview.EventSourcing.MongoDB.Events` | `AddMongoDBEventStore()` | Event stream persistence |
| MongoDB snapshots | `Purview.EventSourcing.MongoDB.Snapshot` | `AddMongoDBSnapshotQueryableEventStore()` | Queryable snapshot store |
| Azure Cosmos DB snapshots | `Purview.EventSourcing.CosmosDb.Snapshot` | `AddCosmosDbQueryableEventStore()` | Queryable snapshot store |

For SQL Server and Azure SQL schema, permissions, and event-versioning guidance, see [docs/sql-server.md](docs/sql-server.md).

## Sample application

The sample solution demonstrates how the framework is intended to be consumed:

- `EventSourcing.Samples.Web` uses the non-generic `IEventStore` and `IQueryableEventStore` facades.
- `EventSourcing.Samples.AppHost` wires up SQL Server, Redis, Azurite, and the web app for Aspire-driven local runs.
- Sample services such as `CartCheckoutService`, `OrderFulfillmentService`, and `StockTransferService` demonstrate multi-aggregate workflows.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/src` | Packable framework packages and shared infrastructure |
| `src/samples` | Sample domain, web app, and Aspire host |
| `src/tests` | Unit, integration, and source generator test projects |
| `docs/sql-server.md` | SQL Server and Azure SQL setup guide |
| `Justfile` | Build, test, format, version, pack, and publish workflow definitions |

## Development workflow

```text
dotnet tool restore
dotnet restore src/Purview.EventSourcing.slnx
dotnet build src/Purview.EventSourcing.slnx --configuration Release
dotnet csharpier check src
dotnet test --project src/tests/EventSourcing.UnitTests/EventSourcing.UnitTests.csproj --configuration Release
```

Additional notes:

- `just` recipes in the `Justfile` wrap the same restore, build, test, pack, and version commands for local development.
- Integration tests use Testcontainers; local Docker support is recommended.
- `package.json` is the release version source of truth for builds and packages.
- `dotnet pack` or `just pack` writes packages to `artifacts/packages`.

## Release workflow

1. Update the package version with the repository release process.
2. Review the generated `CHANGELOG.md` and package version changes.
3. Build, test, and pack the repository.
4. Let the GitHub Actions CD workflow create the tag, publish the GitHub release, and push NuGet packages.

Do not create release tags manually.

## Documentation

- [SQL Server event store guide](docs/sql-server.md)
- [Core package README](src/src/EventSourcing/README.md)
- [Source generator README](src/src/EventSourcing.SourceGenerator/README.md)
- Provider package READMEs in each packable project folder
