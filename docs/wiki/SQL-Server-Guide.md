# SQL Server Event and Snapshot Stores

Purview Event Sourcing ships separate SQL Server-backed event and snapshot implementations in a single NuGet package:

| Package | Class | Purpose |
|---------|-------|---------|
| `Purview.EventSourcing.SqlServer` | `SqlServerEventStore<T>` | Pure event-sourced store — events are the source of truth |
| `Purview.EventSourcing.SqlServer` | `SqlServerSnapshotEventStore<T>` | Queryable snapshot store — optimized for query/list/count over snapshots |

Both stores create their tables automatically on first use (configurable) and use a **single shared table** for all aggregate types.

---

## Table of Contents

1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [Configuration Reference](#configuration-reference)
4. [Required SQL Server Permissions](#required-sql-server-permissions)
5. [Single-Table Design](#single-table-design)
6. [Per-Aggregate Schema and Table Routing](#per-aggregate-schema-and-table-routing)
7. [Event Schema Versioning](#event-schema-versioning)
8. [SQL Transaction Coordination](#sql-transaction-coordination)
9. [Snapshot Payload Shape](#snapshot-payload-shape)
10. [Behavior Notes and Caveats](#behavior-notes-and-caveats)
11. [Connection String Examples](#connection-string-examples)

---

## Installation

```xml
<PackageReference Include="Purview.EventSourcing.SqlServer" />
```

---

## Quick Start

### Events Store

```csharp
// Program.cs
builder.Services.AddSqlServerEventStore();

// appsettings.json
{
  "EventStore:SqlServer": {
    "ConnectionString": "Server=.;Database=MyApp;Trusted_Connection=True;",
    "SchemaName": "dbo",
    "TableName": "EventStore"
  }
}
```

Inject `IEventStore` for the provider-agnostic facade, or `ISqlServerEventStore<T>` when you need the typed SQL Server implementation directly:

```csharp
public class OrderService(IEventStore store)
{
    public async Task PlaceOrderAsync(string orderId, string customerId)
    {
        var order = await store.GetOrCreateAsync<OrderAggregate>(orderId);
        order.CreateOrder(customerId, 0m);
        await store.SaveAsync(order);
    }
}
```

### Event-history API (version and time filters)

The provider-agnostic facade exposes aggregate history reads for audit/review use-cases:

```csharp
var response = await store.GetEventHistoryAsync<OrderAggregate>(
    orderId,
    new AggregateEventHistoryRequest
    {
        FromVersion = 1,
        ToVersion = 200,
        FromUtc = DateTimeOffset.UtcNow.AddDays(-30),
        ToUtc = DateTimeOffset.UtcNow,
        MaxRecords = 100
    },
    cancellationToken);
```

The response is a `ContinuationResponse<AggregateEventHistoryItem>` so callers can page using the returned `ContinuationToken`.

### Snapshot Store

```csharp
// Program.cs
builder.Services.AddSqlServerEventStore();
builder.Services.AddSqlServerSnapshotQueryableEventStore();

// appsettings.json
{
  "EventStore:SqlServerSnapshot": {
    "ConnectionString": "Server=.;Database=MyApp;Trusted_Connection=True;",
    "SchemaName": "dbo",
    "TableName": "Snapshots"
  }
}
```

---

## Configuration Reference

### Events Store (`SqlServerEventStoreOptions`)

| Property | Type | Default | Description |
|---|---|---|---|
| `ConnectionString` | `string` | *(required)* | ADO.NET connection string |
| `SchemaName` | `string` | `"dbo"` | Default schema for the events table |
| `TableName` | `string` | `"EventStore"` | Default table name for events |
| `AutoCreateTable` | `bool` | `true` | Create table and indices on first use |
| `UseDataCompression` | `bool` | `true` | Apply `PAGE` compression (Enterprise / Azure SQL) |
| `TimeoutInSeconds` | `int?` | `60` | Command timeout (1–120 000 s) |
| `MaxEventCountOnSave` | `int` | `1000` | Maximum events per save operation |
| `EventSuffixLength` | `int` | `30` | Zero-padded version suffix on event row IDs |
| `RemoveDeletedFromCache` | `bool` | `true` | Evict deleted aggregates from distributed cache |
| `CacheMode` | `EventStoreCachingOptions` | `GetAndStore` | Distributed-cache interaction policy |
| `DefaultCacheSlidingDuration` | `TimeSpan` | `60 min` | Sliding cache expiry |
| `RequiresValidPrincipalIdentifier` | `bool` | `true` | Require a `ClaimsPrincipal` identifier on save |
| `AggregateTableOverrides` | `Dictionary<string, SqlServerAggregateTableOverride>` | `{}` | Per-aggregate schema/table overrides |

### Snapshot Store (`SqlServerSnapshotEventStoreOptions`)

| Property | Type | Default | Description |
|---|---|---|---|
| `ConnectionString` | `string` | *(required)* | ADO.NET connection string |
| `SchemaName` | `string` | `"dbo"` | Default schema for the snapshots table |
| `TableName` | `string` | `"Snapshots"` | Default table name |
| `AutoCreateTable` | `bool` | `true` | Create table on first use |
| `UseDataCompression` | `bool` | `true` | Apply `PAGE` compression |
| `AggregateTableOverrides` | `Dictionary<string, SqlServerSnapshotAggregateTableOverride>` | `{}` | Per-aggregate schema/table overrides |

---

## Required SQL Server Permissions

### Minimum Runtime Permissions

Grant the application's login (or contained-database user) the following on every schema/table it uses:

```sql
-- On the schema
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[dbo] TO [app_login];

-- Or more targeted, per table:
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[EventStore] TO [app_login];
GRANT SELECT, INSERT, UPDATE, DELETE ON [dbo].[Snapshots]  TO [app_login];
```

### Auto-Create Permissions (`AutoCreateTable = true`)

When `AutoCreateTable` is enabled (the default), the application also needs DDL rights at startup to create the table and its indices:

```sql
-- Required to create tables and indices in the schema:
GRANT CREATE TABLE TO [app_login];
GRANT ALTER  ON SCHEMA::[dbo] TO [app_login];
```

> **Tip:** Use a separate migration user or initialisation step in CI/CD with elevated permissions, then set `AutoCreateTable = false` in production to avoid granting DDL rights to the runtime user.

### Minimal Role-Based Setup (SQL Server)

```sql
-- Create a dedicated role for the event store
CREATE ROLE [event_store_rw];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[dbo] TO [event_store_rw];
ALTER ROLE [event_store_rw] ADD MEMBER [app_login];

-- Additionally for auto-create:
CREATE ROLE [event_store_ddl];
GRANT CREATE TABLE TO [event_store_ddl];
GRANT ALTER ON SCHEMA::[dbo] TO [event_store_ddl];
ALTER ROLE [event_store_ddl] ADD MEMBER [migration_login];
```

### Azure SQL (Managed Identity)

```sql
-- Create contained user for Managed Identity
CREATE USER [my-app-service] FROM EXTERNAL PROVIDER;

ALTER ROLE db_datawriter ADD MEMBER [my-app-service];
ALTER ROLE db_datareader ADD MEMBER [my-app-service];

-- For auto-create only:
GRANT CREATE TABLE TO [my-app-service];
GRANT ALTER ON SCHEMA::[dbo] TO [my-app-service];
```

---

## Single-Table Design

Both stores use a **single shared table** by default. All aggregate types are stored in the same table and distinguished by the `AggregateType` column.

### Events table schema

```
[Id]            NVARCHAR(450)     PK
[EntityType]    INT               0=StreamVersion, 1=Event, 2=IdempotencyMarker, 3=Snapshot
[AggregateId]   NVARCHAR(450)     The aggregate's id
[AggregateType] NVARCHAR(450)     Short name of the aggregate (e.g. "Order")
[Version]       INT               Aggregate version at time of event
[IsDeleted]     BIT               Soft-delete flag on the stream-version row
[Payload]       JSON / NVARCHAR(MAX)  JSON payload (events and snapshots)
[EventType]     NVARCHAR(450)     Mapped event type name (e.g. "Order.CreateOrder")
[IdempotencyId] NVARCHAR(450)     Idempotency marker id
[Timestamp]     DATETIMEOFFSET    UTC timestamp of the operation
```

Three covering indices are created automatically:

| Index | Columns | Purpose |
|---|---|---|
| `IX_EventStore_AggregateId_EntityType` | `(AggregateId, EntityType)` INCLUDE all | Stream lookups |
| `IX_EventStore_EventRange` | `(AggregateId, EntityType, Version)` WHERE EntityType=1 | Event replay |
| `IX_EventStore_AggregateType_EntityType` | `(AggregateType, EntityType, IsDeleted)` INCLUDE AggregateId | Aggregate ID enumeration |

> The single-table design minimises DDL surface area and allows aggregates from different bounded contexts to share a connection pool and database.
>
> **Aggregate ID vs type scoping:** when multiple aggregate types share the same schema/table, event-stream read/delete queries scope by both `AggregateId` and `AggregateType`. If you isolate aggregate types by schema/table via `AggregateTableOverrides`, that physical separation provides the same isolation boundary.

---

## Per-Aggregate Schema and Table Routing

Use `AggregateTableOverrides` to route specific aggregate types to a dedicated schema or table. This is useful when you want bounded-context isolation at the database level while still sharing a connection string.

The dictionary key is the aggregate's **`AggregateType`** value — by convention, the class name with any trailing `Aggregate` suffix stripped (e.g. `"Order"` for `OrderAggregate`). Keys are **case-insensitive**.

### Code-based configuration

```csharp
builder.Services.AddSqlServerEventStore();
builder.Services.Configure<SqlServerEventStoreOptions>(options =>
{
    options.ConnectionString = "Server=.;Database=MyApp;Trusted_Connection=True;";

    // Orders aggregate uses the "orders" schema
    options.AggregateTableOverrides["Order"] = new SqlServerAggregateTableOverride
    {
        SchemaName = "orders",
        TableName  = "EventStore",   // optional — falls back to global TableName
    };

    // Inventory uses a completely separate table
    options.AggregateTableOverrides["Inventory"] = new SqlServerAggregateTableOverride
    {
        SchemaName = "inventory",
        TableName  = "DomainEvents",
    };
});
```

### appsettings.json configuration

```json
{
  "EventStore:SqlServer": {
    "ConnectionString": "Server=.;Database=MyApp;Trusted_Connection=True;",
    "SchemaName": "dbo",
    "TableName": "EventStore",
    "AggregateTableOverrides": {
      "Order":     { "SchemaName": "orders"    },
      "Inventory": { "SchemaName": "inventory", "TableName": "DomainEvents" }
    }
  }
}
```

### How it works

When `SqlServerEventStore<T>` is constructed it looks up `T`'s `AggregateType` name in `AggregateTableOverrides`. If a match is found:
- `SchemaName` override (if set) replaces the global `SchemaName`
- `TableName` override (if set) replaces the global `TableName`
- All other options (compression, timeouts, caching…) are inherited from the global options

Each overridden aggregate type gets its own table with its own set of automatically-created indices.

> **Permissions note:** If you use per-aggregate schema routing you must grant the runtime user `SELECT/INSERT/UPDATE/DELETE` on **each** schema/table used.

---

## Event Schema Versioning

Event classes can declare a **schema version** to track breaking changes to their properties. This allows consumers to perform version-aware deserialization or apply up-casting when replaying old events.

### With the source generator

Set `Version` on `[GenerateAggregateEvent]`:

```csharp
[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    public string CustomerId { get; private set; } = default!;
    public string Currency   { get; private set; } = default!;

    // Version 1: original event (no currency)
    // [GenerateAggregateEvent]            ← implicitly Version = 1
    // public partial void CreateOrder(string customerId);

    // Version 2: added Currency field
    [GenerateAggregateEvent(Version = 2)]
    public partial void CreateOrder(string customerId, string currency);
}
```

The generator emits `public override int SchemaVersion => 2;` in the `OrderCreated` class.

### Manually

Override `SchemaVersion` on any `EventBase` subclass:

```csharp
public sealed class OrderCreated : EventBase
{
    public string CustomerId { get; set; } = default!;
    public string Currency   { get; set; } = default!;

    public override int SchemaVersion => 2;

    protected override void BuildEventHash(ref HashCode hash)
    {
        hash.Add(CustomerId);
        hash.Add(Currency);
    }
}
```

The `SchemaVersion` value is serialized as part of the event's JSON payload. When the event is replayed from the store the version is available via `@event.SchemaVersion`, enabling conditional up-casting:

```csharp
void Apply(OrderCreated e)
{
    CustomerId = e.CustomerId;
    // Up-cast: v1 events did not have Currency; default to "GBP"
    Currency = e.SchemaVersion >= 2 ? e.Currency : "GBP";
}
```

---

## SQL Transaction Coordination

Use `ISqlServerEventStoreTransactionFactory` when you need one SQL Server transaction that includes:

- multiple enlisted aggregates saved through SQL Server event stores,
- extra ad-hoc SQL commands (for example audit/outbox inserts),
- EF Core operations against the same SQL connection boundary.

### Registration and usage

`AddSqlServerEventStore()` registers `ISqlServerEventStoreTransactionFactory`.

```csharp
public sealed class CheckoutService(
    ISqlServerEventStoreTransactionFactory sqlTransactionFactory,
    IEventStore store)
{
    public async Task CheckoutAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        await using var tx = sqlTransactionFactory.CreateSqlServerTransaction();
        tx.Enlist(order, store);

        tx.Enlist(async (connection, sqlTransaction, token) =>
        {
            await using var cmd = new SqlCommand(
                "INSERT INTO dbo.TransactionAudit(CorrelationId, Value) VALUES (@c, @v)",
                connection,
                sqlTransaction);
            cmd.Parameters.AddWithValue("@c", tx.CorrelationId);
            cmd.Parameters.AddWithValue("@v", "checkout");
            await cmd.ExecuteNonQueryAsync(token);
        });

        var result = await tx.CommitAsync(cancellationToken);
        if (!result.Success)
            throw new InvalidOperationException("Transaction failed.");
    }
}
```

### Notes and limits

- SQL-native atomic commit is available when enlisted stores share the same SQL transaction boundary.
- If you need cross-database/distributed coordination, implement a custom transaction coordinator strategy.
- The SQL-specific coordinator requires at least one enlisted aggregate (the aggregate store establishes the connection/transaction boundary).
- `IEventStoreTransactionFactory` remains available and unchanged for provider-agnostic transaction orchestration.

### Integration coverage

`src/tests/EventSourcing.SqlServer.IntegrationTests/Events/SqlServerEventStoreTransactionIntegrationTests.cs` verifies:

- aggregate + raw SQL operation commit in one transaction,
- aggregate + EF operation commit in one transaction,
- rollback of both aggregate and enlisted SQL when an enlisted operation throws.

---

## Snapshot Payload Shape

Snapshot payload is the fully serialized aggregate graph stored in a JSON payload column.

Supported members include:

- writable primitive members,
- `[Scalar]` value objects,
- complex objects composed of supported members,
- `EventStoreList<T>` / `EventStoreSet<T>` collections of supported primitive/complex members.

Unsupported members fail during model creation, including:

- arrays,
- collection types other than `EventStoreList<T>` / `EventStoreSet<T>` (for example `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`, `HashSet<T>`, `ImmutableArray<T>`),
- unsupported object types such as dictionaries.

Read-only and `[JsonIgnore]` members are excluded from snapshot payload mapping.

### Examples

```csharp
// Supported snapshot shape
public sealed class CustomerSnapshot
{
    public string Name { get; set; } = string.Empty;
    public EmailAddress Email { get; set; } = EmailAddress.Hydrate("demo@example.com"); // [Scalar]
    public EventStoreList<OrderLineItem> Items { get; set; } = [];
    public EventStoreSet<string> Tags { get; set; } = [];
}
```

```csharp
// Unsupported snapshot shape (model validation fails)
public sealed class CustomerSnapshot
{
    public List<OrderLineItem> Items { get; set; } = [];       // use EventStoreList<T>
    public string[] Labels { get; set; } = [];                  // arrays unsupported
    public Dictionary<string, string> Metadata { get; set; } = []; // dictionaries unsupported
}
```

For generator/framework behavior (aggregate inheritance paths, hooks, event naming/namespace, manual mode), see [Source Generator Behaviors](Source-Generator-Behaviors.md).

---

## Behavior Notes and Caveats

- `IsDeletedAsync` throws when the aggregate does not exist (it does not return `false` for missing aggregates).
- Event replay is tolerant by default: unknown or unappliable events are skipped, and stream version continues to advance.
- Integration coverage includes replay compatibility scenarios for:
  - **Unknown events** (event type name no longer resolvable): replay skips affected records and continues.
  - **Schema-change style evolution** (event type still deserializes but is no longer applied/registered): replay logs `CannotApplyEvent` and continues.
  - See: `src/tests/EventSourcing.SqlServer.IntegrationTests/Events/SqlServerEventStoreTests.cs`
    and `src/tests/EventSourcing.SqlServer.IntegrationTests/Events/GenericSqlServerEventStoreTests.GetAsync.cs`.
- Principal enforcement is enabled by default (`RequiresValidPrincipalIdentifier = true`), so save operations require the configured claim identifier to be present on the current principal.

---

## Connection String Examples

### Local development (Windows auth)

```
Server=(localdb)\MSSQLLocalDB;Database=MyApp;Trusted_Connection=True;
```

### SQL Server with SQL auth

```
Server=my-server.database.windows.net;Database=MyApp;User Id=app_login;Password=…;
```

### Azure SQL with Managed Identity

```
Server=my-server.database.windows.net;Database=MyApp;Authentication=Active Directory Default;
```

### Azure SQL with connection string from Key Vault

```json
{
  "EventStore:SqlServer": {
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://my-vault.vault.azure.net/secrets/SqlConnection)"
  }
}
```
