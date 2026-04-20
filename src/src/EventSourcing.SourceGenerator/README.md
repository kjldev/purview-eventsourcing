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

## Related packages

- Core framework: `Purview.EventSourcing`
- Repository README: https://github.com/kjldev/purview-eventsourcing/blob/main/README.md
