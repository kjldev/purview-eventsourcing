# Getting Started

## Install

```bash
dotnet add package Purview.EventSourcing
dotnet add package Purview.EventSourcing.SqlServer
```

## Define an aggregate

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

## Register storage

```csharp
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();
```

## Use the provider-agnostic facade

```csharp
public sealed class OrderService(IEventStore store)
{
    public async Task PlaceOrderAsync(string orderId, string customerId, CancellationToken cancellationToken)
    {
        var order = await store.GetAsync<OrderAggregate>(orderId, cancellationToken)
            ?? await store.CreateAsync<OrderAggregate>(orderId, cancellationToken: cancellationToken);

        order.CreateOrder(customerId);
        await store.SaveAsync(order, cancellationToken);
    }
}
```

## Query aggregate event history (time/range filters)

```csharp
var history = await store.GetEventHistoryAsync<OrderAggregate>(
    aggregateId: orderId,
    request: new AggregateEventHistoryRequest
    {
        FromVersion = 10,
        ToVersion = 50,
        FromUtc = DateTimeOffset.UtcNow.AddDays(-7),
        MaxRecords = 100
    },
    cancellationToken: cancellationToken);

foreach (var item in history.Results)
{
    Console.WriteLine($"{item.AggregateVersion} {item.When:u} {item.EventType}");
}
```

## Next pages

- [Source Generator Behaviors](Source-Generator-Behaviors.md)
- [SQL Server Guide](SQL-Server-Guide.md)
- [Release Flow](Release-Flow.md)
