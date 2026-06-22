# Purview EventSourcing Wiki

This wiki-style documentation is the primary project documentation.

## Start here

- [Getting Started](Getting-Started.md)
- [Source Generator Behaviors](Source-Generator-Behaviors.md)
- [SQL Server Guide](SQL-Server-Guide.md)
- [Release Flow](Release-Flow.md)

## Important behaviors

- `[GenerateAggregate]` supports:
  - no declared base class (generator auto-adds `AggregateBase`),
  - direct inheritance from `AggregateBase`,
  - transitive inheritance through one or more intermediate base classes.
- `On<Property>Changing/Changed` hooks are property-scoped across generated events that map the same property.
- `On<Property>Changed` runs in `Apply(...)` (including replay); `On<Property>Changing` runs on command/event-raise path.
- Event-specific hooks (`OnRaising...`, `OnRaised...`, `OnApplied...`) are event-scoped.
- `Manual = true` methods are not auto-wired for property hooks unless invoked manually.

