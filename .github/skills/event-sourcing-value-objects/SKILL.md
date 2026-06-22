---
name: event-sourcing-value-objects
description: Model scalar and complex value objects with source-generated normalization and validation.
category: architecture
roles:
  - architecture
  - coding
  - domain-driven-design
tags:
  - event-sourcing
  - value-objects
  - source-generator
  - validation
---

# Event Sourcing Value Objects Skill

Use this skill when defining value objects in `Purview.EventSourcing` with source-generator support.

## Goals

- Model scalar and non-scalar value objects with strong invariants.
- Normalize incoming values consistently.
- Validate strict creation paths while preserving replay/hydration semantics.
- Support aggregate-aware validation through contextual value object creation.

## Scalar value object rules (`[Scalar]`)

- Use `[Scalar]` on `partial` top-level types.
- Expose a single scalar property (default name `Value`).
- Implement normalization in `static partial void OnNormalize(ref T value)`.
- Implement validation in `static partial void OnValidate(T value)`.
- Use `Create(...)` for strict command-time validation/normalization.
- Use `Hydrate(...)` for replay/deserialization where strict checks should not be re-run.
- Use `ValueObjectDeserializationMode.Strict` only when strict `Create(...)` behavior is required during deserialization.
- Prefer explicit null/empty guards and domain-specific exceptions.

### Scalar template

```csharp
[Scalar]
public readonly partial record struct EmailAddress
{
    public string Value { get; }

    static partial void OnNormalize(ref string value) => value = value?.Trim().ToLowerInvariant()!;

    static partial void OnValidate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email is required.", nameof(value));
    }
}
```

## Non-scalar value object rules (`[ValueObject]`)

- Use `[ValueObject]` on `partial` top-level types.
- Define component properties in the primary constructor or explicit members.
- Normalize all component inputs together in `OnNormalize(ref ...)` before validation.
- Validate business invariants in `OnValidate(...)`.
- Keep non-scalar objects immutable and self-validating.

### Non-scalar template

```csharp
[ValueObject]
public sealed partial record UserDetails(Guid Id, string? DisplayName, bool IsActive = true)
{
    static partial void OnNormalize(ref Guid id, ref string? displayName, ref bool isActive)
    {
        if (!isActive)
            displayName = null;
    }

    partial void OnValidate(Guid id, string? displayName, bool isActive)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must be a valid GUID.", nameof(id));
    }
}
```

## Contextual value objects for aggregate-aware validation

- Implement `IContextualValueObject<TSelf, TValue, TAggregate>` when validity depends on current aggregate state.
- Implement `Create(TValue value, in ValueObjectContext<TAggregate> context)` to enforce state-machine transitions or cross-field constraints.
- Keep `Hydrate(...)` available for replay paths that should not fail on historical data.
- Use context fields (`Aggregate`, `MemberName`, `EventName`) to scope validation logic and diagnostics.

### Contextual template

```csharp
[Scalar]
public readonly partial record struct OrderStatus
    : IContextualValueObject<OrderStatus, OrderStatusCode, OrderAggregate>
{
    public OrderStatusCode Value { get; }

    public static OrderStatus Create(OrderStatusCode value, in ValueObjectContext<OrderAggregate> context)
    {
        var current = context.Aggregate.Status.Value;
        return IsValidTransition(current, value)
            ? new(value)
            : throw new InvalidOperationException($"Invalid transition {current} -> {value}");
    }
}
```

## Decision guide: where validation lives

- Put primitive-format and canonicalization rules in value objects (`OnNormalize`/`OnValidate`).
- Put aggregate lifecycle/state-machine rules in aggregate hooks and contextual `Create(...)`.
- Keep replay-safe behavior by separating strict creation (`Create`) from hydration (`Hydrate`).

## Output template to use

1. Value object catalog (scalar vs non-scalar vs contextual).
2. Normalization rules per input member.
3. Validation/invariant rules with expected exceptions.
4. Strict create vs hydrate behavior.
5. Aggregate-context dependencies (if contextual).

