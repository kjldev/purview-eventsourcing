# Purview.EventSourcing.SourceGenerator

`Purview.EventSourcing.SourceGenerator` is the Roslyn source generator for Purview EventSourcing aggregates. It generates aggregate event classes, event registration code, and typed partial command methods from attributed aggregate definitions.

## Install

```bash
dotnet add package Purview.EventSourcing.SourceGenerator
```

This package is typically used alongside the core `Purview.EventSourcing` package.

## What it generates

- `RegisterEvents()` boilerplate for attributed aggregates
- Event classes for methods marked with `[GenerateAggregateEvent]`
- Typed partial method implementations that raise and apply generated events

## Typical usage

```csharp
using Purview.EventSourcing.Aggregates;

[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    [GenerateAggregateEvent]
    public partial void CreateOrder(string customerId);
}
```

## Supported aggregate shape

- `[GenerateAggregate]` must be applied to a non-nested, non-generic `partial` class.
- The aggregate must inherit from `Purview.EventSourcing.Aggregates.AggregateBase`.
- Do not implement `RegisterEvents()` yourself; the generator owns that override.

## Supported generated event method shape

- `[GenerateAggregateEvent]` methods must be declared on a `[GenerateAggregate]` aggregate.
- Methods must be `public partial void` declarations without a body.
- Static methods, generic methods, overloads, and `ref` / `in` / `out` / `params` parameters are not supported.

## Parameter-to-property mapping

- Each generated event method parameter maps to an aggregate property by upper-casing the first character of the parameter name.
- Example: `customerId` maps to `CustomerId`.
- The mapped property must exist on the aggregate, have a non-`init` setter, and accept the parameter type via implicit assignment.

## Common diagnostics

- `PVEVTGEN001`-`PVEVTGEN005`: invalid aggregate declarations
- `PVEVTGEN006`-`PVEVTGEN009`: invalid generated-event method usage
- `PVEVTGEN010`: a generated event parameter does not map cleanly to writable aggregate state

## Related packages

- Core framework: `Purview.EventSourcing`
- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
