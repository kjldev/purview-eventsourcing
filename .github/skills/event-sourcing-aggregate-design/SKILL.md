---
name: event-sourcing-aggregate-design
description: Define and model event-sourced aggregates, events, and state transitions.
category: architecture
roles:
  - architecture
  - coding
  - domain-driven-design
tags:
  - event-sourcing
  - aggregates
  - domain-modeling
  - ddd
---

# Event Sourcing Aggregate Design Skill

Use this skill when designing or refining an event-sourced domain model.
For deeper implementation details, pair with:

- `event-sourcing-value-objects`
- `event-sourcing-aggregate-validation`

## Goals

- Define aggregate boundaries and invariants.
- Convert business state and property changes into explicit domain events.
- Produce clear aggregate property names, event names, and transition rules.
- Support implementation guidance for event-sourcing solutions.
- Follow source-generator-first patterns for aggregate/event implementation.

## Rules for aggregate design

- Model one aggregate as one consistency boundary.
- Keep invariants inside the aggregate; do not rely on external checks for core rules.
- Accept commands, validate business rules, and emit events.
- Rebuild state only by replaying events.
- Do not store mutable aggregate state as source-of-truth outside event history.
- Keep events immutable and append-only.

## Source-generator implementation rules (Purview.EventSourcing style)

- Mark aggregate roots with `[GenerateAggregate]`.
- Aggregates must be `partial`.
- Inheritance options for `[GenerateAggregate]`:
  - no declared base class (generator auto-adds `AggregateBase` on the generated partial type),
  - direct inheritance from `AggregateBase`,
  - transitive inheritance through one or more intermediate base classes.
- Define command methods as `partial` methods and annotate with `[GenerateAggregateEvent]`.
- Prefer explicit event naming with `EventName = "..."` when domain language needs to differ from method name.
- Use `Version = <n>` on `[GenerateAggregateEvent]` for schema evolution.
- Use `[GenerateAggregateCollectionEvent(nameof(CollectionProperty))]` for list/set mutation events.
- Use `[Computed]` for values derived inside the aggregate and not provided by callers.
- Keep aggregate collections as `EventStoreList<T>` or `EventStoreSet<T>` for generated collection-event patterns.
- Use partial hooks for invariants and side effects:
  - `OnRaising<EventName>Event(ref ...)` for pre-emit validation/mutation.
  - `OnRaised<EventName>Event(@event)` after event creation.
  - `OnApplied<EventName>Event(@event)` after state apply.
  - `On<Property>Changing(ref value)` and `On<Property>Changed(previous, current)` for property transitions.
- Use `Manual = true` only when providing a manual event-body implementation is required.
- Do not hand-roll registration boilerplate already generated (event types, registration, apply plumbing).
- Prefer invariant enforcement in aggregate hooks and contextual value-object `Create(...)` methods.

### Property-hook behavior rules

- `On<Property>Changing/Changed` are property-scoped and apply regardless of which generated event updates that property.
- `On<Property>Changing` executes before event creation on command methods.
- `On<Property>Changed` executes in generated `Apply(...)` methods and therefore runs during replay.
- Hooks are invoked only for properties mapped by that event method.
- Event-specific hooks (`OnRaising...`, `OnRaised...`, `OnApplied...`) are event-scoped, not property-scoped.
- `Manual = true` methods do not auto-wire these hooks unless invoked explicitly in manual code.

## Source-generator aggregate skeleton

```csharp
[GenerateAggregate]
public sealed partial class OrderAggregate : AggregateBase
{
    public string CustomerId { get; private set; } = string.Empty;
    public EventStoreSet<string> Tags { get; private set; } = [];

    [GenerateAggregateEvent(EventName = "OrderCreated", Version = 1)]
    public partial OrderAggregate Create(string customerId);

    [GenerateAggregateCollectionEvent(nameof(Tags))]
    public partial OrderAggregate AddTag(string tag);

    partial void OnRaisingOrderCreatedEvent(ref string customerId)
    {
        // validate invariant before event is raised
    }

    partial void OnAppliedOrderCreatedEvent(OrderEvents.OrderCreated @event)
    {
        // optional post-apply behavior
    }
}
```

## Naming conventions

- Aggregate names: singular noun (for example: `Order`, `Invoice`, `Subscription`).
- Event names: past tense, business meaning first (for example: `OrderPlaced`, `PaymentCaptured`, `SubscriptionCancelled`).
- Command names: intent/action phrasing (for example: `PlaceOrder`, `CapturePayment`, `CancelSubscription`).
- Properties: domain language and explicit meaning (`CurrentStatus`, `TotalAmount`, `Revision`, `OccurredAtUtc`), avoid vague names (`Data`, `Value`, `Info`).

## Property/state to event mapping process

1. Identify the business decision or fact that changed.
2. Define the command that requests that decision.
3. Validate invariants and preconditions.
4. Emit one or more domain events that describe facts in past tense.
5. Update aggregate state by applying those events.
6. Confirm each new property is derivable from event history.

## Support checklist for event-sourcing solutions

- Aggregate boundary and invariant list is explicit.
- Command handlers return domain errors for invalid transitions.
- Each event carries enough data to rebuild state without hidden dependencies.
- Event versioning and compatibility strategy is defined.
- Snapshots (if used) are optimization only, never source-of-truth.
- Idempotency and optimistic concurrency expectations are defined.
- Projection/read-model requirements are separated from write model concerns.

## Output template to use

When applying this skill, provide:

1. Aggregate definition (purpose, boundary, invariants).
2. Source-generator contract (`[GenerateAggregate]`, `partial` commands, hook methods).
3. Command list with validation rules.
4. Event catalog with payload schema, version, and naming rationale.
5. State transition table (current state + command -> event(s) + new state).
6. Implementation notes (generated apply/replay flow, concurrency, versioning, snapshots).
