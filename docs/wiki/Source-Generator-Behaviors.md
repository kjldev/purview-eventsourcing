# Source Generator Behaviors

This page documents framework-level source-generator behavior (not storage-provider behavior).

## Aggregate eligibility and inheritance

`[GenerateAggregate]` supports three inheritance paths:

1. No declared base class: generated partial type automatically inherits `AggregateBase`.
2. Direct inheritance from `AggregateBase`.
3. Transitive inheritance through one or more intermediate base classes.

Other eligibility rules:

- Aggregate type must be `partial`.
- Nested and generic aggregate types are not supported.
- `RegisterEvents()` is generated and cannot be manually declared.

### Examples

```csharp
// 1) No declared base class (generator adds AggregateBase on generated partial)
[GenerateAggregate]
public partial class ProductAggregate
{
    [GenerateAggregateEvent]
    public partial void Create(string name);
}

// 2) Direct inheritance
[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    [GenerateAggregateEvent]
    public partial void CreateOrder(string customerId);
}

// 3) Transitive inheritance
public abstract class DomainAggregateBase : AggregateBase { }
public abstract class BillingAggregateBase : DomainAggregateBase { }

[GenerateAggregate]
public partial class InvoiceAggregate : BillingAggregateBase
{
    [GenerateAggregateEvent]
    public partial void CreateInvoice(string invoiceNumber);
}
```

## Generated event naming and namespace

Default event namespace:

- `<AggregateNamespace>.<AggregateNameWithoutSuffix>Events`
- Example: `Testing.OrderAggregate` -> `Testing.OrderEvents`

Default event type naming:

- Event names are inferred from method names (or overridden with `EventName = ...`).
- Event type suffix defaults to `Event` (configurable with `EventSuffix` defaults/overrides).
- Typical generated type: `Testing.OrderEvents.OrderCreatedEvent`.

Namespace can be overridden per method (`EventNamespace`) or by aggregate defaults.

### Examples

```csharp
namespace Testing;

[GenerateAggregate]
public partial class OrderAggregate : AggregateBase
{
    [GenerateAggregateEvent]
    public partial void CreateOrder(string customerId);

    [GenerateAggregateEvent(EventName = "OrderRegistered", EventNamespace = "Testing.Custom.Events")]
    public partial void RegisterOrder(string customerId);
}
```

Typical generated types:

- `Testing.OrderEvents.OrderCreatedEvent` (default namespace/name)
- `Testing.Custom.Events.OrderRegisteredEvent` (explicit namespace/name)

## Hook behavior semantics

Property hooks are property-scoped:

- `On<Property>Changing(ref value)` runs on generated command methods before event creation.
- `On<Property>Changed(previous, current)` runs in generated `Apply(...)` after assignment.
- If different events update the same property, the same property hooks run for each.
- Hooks run only when the event method maps that property.

Replay behavior:

- Replay executes generated `Apply(...)`.
- `On<Property>Changed` runs on replay.
- `On<Property>Changing` does not run on replay.

Event hooks are event-scoped:

- `OnRaising<EventName>Event(ref ...)`
- `OnRaised<EventName>Event(@event)`
- `OnApplied<EventName>Event(@event)`
- `OnShouldApply<EventName>Event(@event, ref bool shouldApply)`

Manual behavior:

- `Manual = true` does not auto-wire property hooks unless manual code invokes them.

### Example

```csharp
[GenerateAggregate]
public partial class CustomerAggregate : AggregateBase
{
    public string Email { get; private set; } = string.Empty;

    [GenerateAggregateEvent(EventName = "CustomerRegistered")]
    public partial void Register(string email);

    [GenerateAggregateEvent(EventName = "CustomerEmailChanged")]
    public partial void ChangeEmail(string email);

    partial void OnEmailChanging(ref string email) => email = email.Trim().ToLowerInvariant();
    partial void OnEmailChanged(string previous, string current) { /* audit */ }
}
```

`OnEmailChanging/Changed` run for both `Register` and `ChangeEmail` because both map to `Email`.

## Event method mapping and validation

- `[GenerateAggregateEvent]` methods must be `partial` declarations without bodies.
- Return types must be `void`, `bool`, or the containing aggregate type.
- Parameters must map to writable aggregate properties unless explicitly handled as metadata/manual payload.
- Collection event methods (`[GenerateAggregateCollectionEvent]`) require `EventStoreList<T>` / `EventStoreSet<T>` target properties.

### Example

```csharp
[GenerateAggregate]
public partial class ReportAggregate : AggregateBase
{
    public EventStoreSet<string> Tags { get; private set; } = [];

    [GenerateAggregateCollectionEvent(nameof(Tags))]
    public partial void AddTag(string tag);
}
```

## Value-object conversion behavior

- Generated mapping paths use `Create(...)` semantics for strict command-time conversion/validation.
- Contextual `Create(TValue, in ValueObjectContext<TAggregate>)` is used when available.
- Replay/hydration paths apply event payloads through generated `Apply(...)` logic.

### Examples

```csharp
// Scalar conversion
[Scalar]
public readonly partial record struct EmailAddress
{
    public string Value { get; }
    static partial void OnNormalize(ref string value) => value = value.Trim().ToLowerInvariant();
    static partial void OnValidate(string value) { /* format checks */ }
}
```

```csharp
// Contextual conversion
[Scalar]
public readonly partial record struct OrderStatus
    : IContextualValueObject<OrderStatus, OrderStatusCode, OrderAggregate>
{
    public OrderStatusCode Value { get; }

    public static OrderStatus Create(OrderStatusCode value, in ValueObjectContext<OrderAggregate> context)
        => IsValidTransition(context.Aggregate.Status.Value, value)
            ? new(value)
            : throw new InvalidOperationException();
}
```

## Diagnostics to expect

Common diagnostic IDs:

- `EVENTSTORE001` aggregate must be partial
- `EVENTSTORE002` aggregate must inherit `AggregateBase` (or have no base so generator can add it)
- `EVENTSTORE003` nested aggregates unsupported
- `EVENTSTORE004` generic aggregates unsupported
- `EVENTSTORE005` manual `RegisterEvents` unsupported
- `EVENTSTORE007` generated event method must be partial
- `EVENTSTORE010` parameter must map to writable property
- `EVENTSTORE018` unsupported aggregate collection property type
