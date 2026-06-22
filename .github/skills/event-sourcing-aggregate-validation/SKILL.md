---
name: event-sourcing-aggregate-validation
description: Apply aggregate validation patterns for command, save, and persistence-time guards.
category: architecture
roles:
  - architecture
  - coding
  - domain-driven-design
tags:
  - event-sourcing
  - aggregate
  - validation
  - fluentvalidation
---

# Event Sourcing Aggregate Validation Skill

Use this skill when defining validation strategy for event-sourced aggregates in `Purview.EventSourcing`.

## Validation layers to apply

1. **Command/event-raising validation**
   - Enforce transition and intent rules in generated hooks (`OnRaising...Event`, `On<Property>Changing`).
   - Reject invalid operations before events are recorded.

2. **Aggregate model validation**
   - Use DataAnnotations on aggregate properties for structural constraints.
   - Examples: `[Range]`, `[Required]`, length constraints.

3. **Save-time validation**
   - Save pipeline validates aggregates through `IAggregateValidator<TAggregate>`.
   - If no custom validator is provided, `DefaultAggregateValidator<TAggregate>` runs DataAnnotations validation.
   - If a FluentValidation `IValidator<TAggregate>` is provided, it is adapted via `FluentValidationAggregateValidator<TAggregate>`.

## Rules

- Keep invariant checks close to mutation points (command/hook layer).
- Keep structural and shape constraints declarative (DataAnnotations/FluentValidation).
- Do not silently swallow validation failures.
- Treat `SaveResult<TAggregate>.IsValid` and `ValidationResult` as first-class outcomes.
- Use `SaveResult.EnsureValid()` when invalid saves must throw immediately.
- Preserve replay tolerance by avoiding command-time assumptions in hydration-only paths.

## Aggregate validation checklist

- Every command has explicit precondition checks.
- Property transitions are guarded (`On<Property>Changing` / `On<Property>Changed` where needed).
- Aggregate-level attributes capture simple declarative constraints.
- Custom FluentValidation rules are used for cross-field/business policies not suited to attributes.
- Save handlers inspect or enforce `SaveResult.ValidationResult`.

## Output template to use

1. Validation map by layer (command, aggregate model, save pipeline).
2. Hook methods and invariants per command.
3. Attribute-based constraints and rationale.
4. FluentValidation rules (if any) and when they execute.
5. SaveResult handling policy (`IsValid`, `EnsureValid`, error propagation).

