# Purview EventSourcing

A comprehensive, production-ready event sourcing framework for .NET with support for multiple storage backends.

## Packages

| Package | Description |
|---------|-------------|
| `Purview.EventSourcing` | Core abstractions: `IEventStore<T>`, `AggregateBase`, `EventBase` |
| `Purview.EventSourcing.SourceGenerator` | Roslyn source generator for boilerplate-free aggregate definitions |
| `Purview.EventSourcing.SqlServer.Events` | SQL Server / Azure SQL event store |
| `Purview.EventSourcing.SqlServer` | SQL Server / Azure SQL queryable snapshot store |
| `Purview.EventSourcing.AzureStorage` | Azure Table Storage event store |
| `Purview.EventSourcing.MongoDb.Events` | MongoDB event store |
| `Purview.EventSourcing.MongoDb.Snapshot` | MongoDB queryable snapshot store |
| `Purview.EventSourcing.CosmosDb.Snapshot` | Azure Cosmos DB queryable snapshot store |

## Quick Start

### 1. Define an aggregate

```csharp
using Purview.EventSourcing.Aggregates;

[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    public string  CustomerId { get; private set; } = default!;
    public decimal Total      { get; private set; }

    [GenerateAggregateEvent]
    public partial void CreateOrder(string customerId, decimal total);

    [GenerateAggregateEvent]
    public partial void UpdateTotal(decimal total);
}
```

The `[GenerateAggregate]` / `[GenerateAggregateEvent]` source generator creates the event classes, `RegisterEvents()`, `Apply()` methods and partial command method bodies automatically.

### 2. Register the store

```csharp
builder.Services.AddSqlServerEventStore();
```

```json
{
  "EventStore:SqlServer": {
    "ConnectionString": "Server=.;Database=MyApp;Trusted_Connection=True;"
  }
}
```

### 3. Use the store

```csharp
public class OrderService(IEventStore<OrderAggregate> store)
{
    public async Task PlaceOrderAsync(string orderId, string customerId, decimal total)
    {
        var order = await store.GetOrCreateAsync(orderId);
        order.CreateOrder(customerId, total);
        await store.SaveAsync(order);
    }
}
```

### 4. Coordinate a transaction

```csharp
public class CheckoutService(
    IEventStoreTransactionFactory transactionFactory,
    IEventStore<OrderAggregate> orderStore,
    IEventStore<InventoryAggregate> inventoryStore)
{
    public async Task CheckoutAsync(OrderAggregate order, InventoryAggregate inventory)
    {
        await using var transaction = transactionFactory.Create();
        transaction.Enlist(order, orderStore);
        transaction.Enlist(inventory, inventoryStore);

        await transaction.CommitAsync();
    }
}
```

`EventStoreTransaction` keeps the client API stable while automatically choosing the strongest compatible coordinator underneath. When all enlisted stores share the same native transaction boundary, the framework uses the provider-specific transaction layer; otherwise it falls back to the shared-correlation logical transaction model.

## Documentation

- [SQL Server — setup, permissions, schema routing, versioning](docs/sql-server.md)

## Building

```bash
dotnet build src/Purview.EventSourcing.slnx --configuration Release
```

## Testing

```bash
dotnet test src/Purview.EventSourcing.slnx
```

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) and require Docker.

### Filtering Tests

Tests use [TUnit](https://tunit.dev). To filter by test name, class, or namespace, use `--treenode-filter` passed after `--`:

```bash
# Run a specific test by name
dotnet test src/Purview.EventSourcing.slnx -- --treenode-filter "/*/*/*/MyTestName*"

# Run all tests in a class
dotnet test src/Purview.EventSourcing.slnx -- --treenode-filter "/*/*/MyClassName/*"

# Run all tests in a namespace
dotnet test src/Purview.EventSourcing.slnx -- --treenode-filter "/*/My.Namespace.Tests/*/*"

# Filter by test property (e.g. category)
dotnet test src/Purview.EventSourcing.slnx -- --treenode-filter "/*/*/*/*[Category=Smoke]"
```

Filter format: `/<Assembly>/<Namespace>/<ClassName>/<TestName>`. Use `*` as a wildcard at any segment.

### Running Tests Serially

To disable parallelism (useful for debugging flaky tests):

```bash
dotnet test src/Purview.EventSourcing.slnx -- --maximum-parallel-tests 1
```
