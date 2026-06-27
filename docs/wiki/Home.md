# Purview EventSourcing Wiki

This wiki is the project documentation hub for framework features, provider capabilities, and release workflow.

## Start here

- [Getting Started](Getting-Started.md)
- [Solution Design Guide](Solution-Design-Guide.md)
- [Solution Design Worksheet](Solution-Design-Worksheet.md)
- [Provider Feature Matrix](Provider-Feature-Matrix.md)
- [Source Generator Behaviors](Source-Generator-Behaviors.md)
- [SQL Server Guide](SQL-Server-Guide.md)
- [Release Flow](Release-Flow.md)

## Feature highlights

- **Core framework (`Purview.EventSourcing`)**
  - `AggregateBase`, `IEventStore`, `IQueryableEventStore`, and `IEventStoreTransactionFactory`.
  - Source-generated aggregate events/command wiring from partial methods.
  - Provider-agnostic aggregate load/save/query APIs.
- **Solution design**
  - Paper-first worksheets for aggregate boundaries, commands, events, relationships, and event streams.
  - Guidance for relational data, value objects, validation layers, and schema evolution.
- **Storage providers**
  - SQL Server / Azure SQL: event streams + queryable snapshots + SQL transaction coordination.
  - Azure Storage: table-backed event streams with blob support for snapshots/large payloads.
  - MongoDB: event streams + queryable snapshots.
  - Cosmos DB: queryable snapshot store.
- **Generator behavior**
  - `[GenerateAggregate]` supports no base, direct `AggregateBase`, and transitive base-chain inheritance.
  - Property hooks are property-scoped across generated events that map that property.
  - `On<Property>Changed` runs in `Apply(...)` (including replay); `On<Property>Changing` runs on command/event-raise path only.
  - Event hooks (`OnRaising...`, `OnRaised...`, `OnApplied...`) are event-scoped.
