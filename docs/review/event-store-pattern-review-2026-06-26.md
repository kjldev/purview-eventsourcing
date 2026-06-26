# Purview EventSourcing — Event Store Pattern Review (2026-06-26)

## Scope

This review evaluates the repository against the **canonical Event Store pattern** (stream-first event persistence, append-only semantics, optimistic concurrency, idempotent writes, replayability, versioning/upcasting, projections, and subscription/change-feed behavior).

Baseline reviewed from core contracts, provider implementations, docs, and tests.

## Executive verdict

**Overall: Strong alignment with Event Store pattern principles, with a few important standardization gaps across providers and operational hardening opportunities.**

In short: this is not just “event-sourcing flavored CRUD” — it is a genuine event-store-oriented design with meaningful transactional, idempotency, replay, and versioning features.

## Pattern comparison (industry/Event Store expectations)

| Event Store pattern capability | Expected pattern behavior | Current status | Evidence |
| --- | --- | --- | --- |
| Stream-first source of truth | Aggregate state rebuilt from event stream; snapshots are optimization only | ✅ Strong | `IEventStore*` contracts and provider `Get...` + replay behavior; snapshots are optional and skippable via `SkipSnapshot`. |
| Append-only events | New events appended; historical events not overwritten | ✅ Strong | Save flows persist event rows/docs per version and update stream metadata; docs and contracts emphasize non-overwrite semantics. |
| Optimistic concurrency | Write fails when stream head changed concurrently | ✅/⚠️ Good (provider variance) | SQL/Azure map storage conflicts to `ConcurrencyException`; Mongo currently maps to generic commit failure path. |
| Idempotent write support | Duplicate request detection via operation identity/marker | ✅ Strong | `EventStoreOperationContext.UseIdempotencyMarker`; provider marker entities and duplicate short-circuit behavior. |
| Correlation/causation metadata | Correlation across multi-write workflows | ✅ Good | `CorrelationId` propagated via operation context and transaction orchestration. |
| Replay/time-travel | Rehydrate at version and enumerate ranges | ✅ Strong | `GetAtAsync`, `GetEventRangeAsync`, history request model with version/time windows. |
| Event versioning/upcasting | Schema version + migration chain | ✅ Strong | `EventBase.SchemaVersion`, `GenerateAggregateEvent(Version=...)`, `EventUpcasterRegistry` chain support with cycle guard. |
| Projections/read model separation | Write model separate from query model | ✅ Good | `IQueryableEventStore` abstraction and provider matrix with snapshot query stores. |
| Subscription/change feed | Push/pull mechanisms for downstream processors | ✅/⚠️ Good | `IAggregateChangeFeedProcessor*` hooks provide change notifications; durable subscription semantics are less explicit. |
| Multi-aggregate consistency | Transaction coordination semantics clearly defined | ✅ Good | `EventStoreTransaction` supports native atomic mode when boundary matches; documented fallback is eventual consistency. |

## What aligns very well with Event Store pattern

1. **Clear stream semantics**
   - Contracts expose `GetAtAsync`, `GetEventRangeAsync`, and history request paging/filtering.
   - This is core Event Store behavior, not an afterthought.

2. **Pragmatic idempotency support**
   - Marker-based dedupe is available and integrated with operation context and transaction orchestration.

3. **First-class event evolution story**
   - Schema version on events + explicit upcaster chain registry.
   - Supports multi-hop migration and replay safety.

4. **Good transactional model articulation**
   - Native atomic path when store boundary is shared.
   - Explicit eventual-consistency fallback for mixed boundaries.

5. **Snapshots treated as optimization**
   - `SkipSnapshot` allows full replay path and preserves event stream as source of truth.

## Key gaps vs best-in-class Event Store implementations

1. **Provider consistency gap for concurrency taxonomy**
   - SQL/Azure explicitly surface concurrency exceptions from provider conflicts.
   - Mongo path is more commit-oriented and less explicit for concurrency failure classification.

2. **Ambiguity/TODO in rehydration logic comments (Mongo/Azure `GetAsync`)**
   - Known unresolved comments indicate uncertainty around stream version selection in replay path.
   - This is risky in an Event Store because replay correctness is foundational.

3. **Durable subscription semantics not fully explicit**
   - Change feed hooks are good, but checkpointing/replay-from-position behavior is not clearly positioned like classic durable subscriptions.

4. **Operational governance still behind top OSS Event Store projects**
   - Missing policy docs (`SECURITY.md`, contributor/governance files) reduce ecosystem trust, despite strong core engineering.

## MoSCoW backlog (tasks / features / issues) — Event Store pattern focused

### Must have

1. **[Issue] Normalize concurrency exception behavior across providers**
   - Ensure Mongo provider distinguishes optimistic concurrency conflicts from generic commit failures.
   - Outcome: consistent retry policy handling for clients.

2. **[Issue] Resolve replay/stream-version TODO logic in provider `GetAsync` paths**
   - Files include Mongo/Azure provider `GetAsync` implementations.
   - Outcome: deterministic, documented replay semantics.

3. **[Task] Add explicit Event Store semantics docs**
   - Define expected-version behavior, idempotency guarantees, ordering guarantees, and failure contracts per provider.
   - Outcome: predictable cross-provider behavior and safer adoption.

4. **[Task] Add OSS security/governance baseline docs**
   - `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `.github/CODEOWNERS`.
   - Outcome: project trust and maintainability uplift.

### Should have

1. **[Feature] Durable subscription/checkpoint reference implementation**
   - Provide a canonical processor with checkpoint persistence and replay-from-checkpoint.
   - Outcome: stronger Event Store downstream-processing story.

2. **[Feature] Explicit causal metadata guidance**
   - Extend documentation for correlation/causation usage conventions and propagation policies.
   - Outcome: improved traceability across bounded contexts.

3. **[Task] Event contract compatibility tests**
   - Add replay/upcast compatibility matrix tests across event versions.
   - Outcome: safer schema evolution.

4. **[Task] Provider parity scorecard in docs**
   - Add pattern-specific parity dimensions (concurrency, idempotency, transactionality, snapshot mode, history query guarantees).
   - Outcome: better architecture decision support.

### Could have

1. **[Feature] Position-based stream read API (global/event-log style)**
   - Add optional global ordered read model for integration/event-forwarding use cases.

2. **[Feature] Subscription primitives beyond hook callbacks**
   - Pull-based cursor APIs and server-side filtering patterns.

3. **[Task] Perf + contention benchmarks by provider**
   - Include high-contention optimistic-concurrency scenarios and dedupe marker overhead.

4. **[Task] Observability conventions package**
   - Standard metrics/traces naming for save/replay/idempotency/concurrency events.

### Won’t have now

1. **[Task] Enforce identical transactional guarantees for all providers**
   - Not realistic due to backend capability differences; document guarantee tiers instead.

2. **[Feature] Fully managed cloud event-stream service abstraction in this cycle**
   - Current package focus is framework + provider implementations.

## Suggested implementation sequence

1. Replay TODO fixes + tests (correctness first)
2. Concurrency taxonomy parity (Mongo alignment)
3. Event Store semantics documentation and provider guarantee table
4. Durable subscription/checkpoint sample
5. Governance/security docs and ecosystem hardening

## Evidence notes

- Core contracts: `IEventStore.cs`, `IEventStoreCore.cs`, `EventStoreOperationContext.cs`, `IEventStoreTransaction.cs`
- Versioning/upcasting: `EventBase.cs`, `EventUpcasterRegistry.cs`
- Change feed: `IAggregateChangeFeedProcessor.cs`
- Provider save paths: SQL/Mongo/Azure `*SaveAsync.cs`
- Existing validation run context: 459 tests passed on current branch (net10/TUnit run)

## Final assessment

From an Event Store pattern standpoint, the project is **architecturally strong and credible**. The main work now is **parity hardening and semantic explicitness** (especially replay correctness and concurrency consistency), not foundational redesign.
