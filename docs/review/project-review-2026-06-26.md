# Purview EventSourcing — Project Review (2026-06-26)

## Scope

This review covers the repository at `kjldev/purview-eventsourcing` (branch: `cleaning`) across:

- Architecture and code quality
- Build, test, and release engineering
- Security and supply-chain posture
- Documentation and contributor experience
- Alignment with common industry standards and OSS best practices

## Method and evidence

Reviewed repository structure, core implementation and workflow files, including:

- `README.md`, `docs/wiki/*`
- `.github/workflows/*.yml`
- `global.json`, `Directory.Packages.props`, `.editorconfig`, `Justfile`, `commitlint.config.mts`
- Representative core files (e.g. `src/src/EventSourcing/Aggregates/AggregateBase.cs`, `src/src/EventSourcing/EventStoreTransaction.cs`)
- Representative provider files and tests

Validation run executed:

- `dotnet test src/Purview.EventSourcing.slnx --configuration Release -- --treenode-filter "/*UnitTest*/*/*/*" --report-trx --ignore-exit-code 8`
- Result: **459 passed, 0 failed, 0 skipped**

## Executive summary

The project is in a **strong, release-capable state** and demonstrates above-average engineering maturity for an OSS .NET framework.

### What stands out positively

- Clear modular architecture (core + provider packages + generator + samples)
- Excellent test breadth (unit, integration, source-generator, performance harnesses)
- Mature release pipeline (guards, duplicate checks, OIDC trusted publishing)
- Good developer ergonomics (central package mgmt, just recipes, lint hooks, commit convention)
- Good API design direction for event sourcing abstractions and transaction coordination

### Primary gaps vs best-in-class

- Missing community/governance baseline files (`SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CODEOWNERS`)
- No automated dependency update workflow (`dependabot.yml` or Renovate)
- A few known TODOs in provider rehydration/options validation paths are still unresolved
- Sample value-object definitions currently generate build warnings (CS9113)

## Comparison to industry standards

| Area | Industry expectation | Current status | Notes |
| --- | --- | --- | --- |
| CI quality gate | Restore/build/test + surfaced test reports | ✅ Strong | Reusable workflows and PR result publishing are implemented well. |
| Release safety | Guarded tagging, duplicate detection, provenance-safe publish | ✅ Strong | `release.yml` has branch guards, tag/release checks, NuGet duplicate checks, OIDC trusted publishing. |
| Testing strategy | Layered tests incl. integration where relevant | ✅ Strong | 459 tests passed; includes integration tests per provider and source generator coverage. |
| Dependency governance | Automated update cadence + advisory handling | ⚠️ Partial | Central versions are good, but no Dependabot/Renovate automation found. |
| Security policy | Public disclosure process and support window docs | ❌ Gap | `SECURITY.md` not present. |
| OSS onboarding | Contribution and conduct guidance | ❌ Gap | `CONTRIBUTING.md` and `CODE_OF_CONDUCT.md` not found. |
| Ownership model | CODEOWNERS for review boundaries | ❌ Gap | `CODEOWNERS` not found. |
| Static quality enforcement | Lint/format/analyzers as PR blockers | ✅/⚠️ Good with gaps | Strong style config/hooks; workflow warns on context validity in `release.yml` env secret reference. |
| Documentation depth | Architecture + usage + release docs | ✅ Strong | README and wiki documentation quality is high. |
| Operational observability | Telemetry conventions and failure handling | ✅ Good | Core/provider code demonstrates telemetry hooks and failure reporting patterns. |

## Key findings (tasks / features / issues) in MoSCoW format

### Must have (high priority, near-term)

1. **[Issue] Add OSS governance baseline files**
   - Add: `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, and `.github/CODEOWNERS`.
   - Why: These are standard trust and maintainability signals for public projects.

2. **[Issue] Resolve workflow validation warning in release pipeline**
   - Observed warning in `release.yml` around context access for `RELEASE_BOT_PRIVATE_KEY`.
   - Why: Reduce false positives and ensure workflow lint clarity/portability.

3. **[Issue] Eliminate sample build warnings (CS9113)**
   - File: `src/src/EventSourcing.Samples/ValueObjects/UserCapture.cs`.
   - Why: Keep warning budget at zero for clean CI and better signal-to-noise.

4. **[Task] Formalize security response process**
   - Include vulnerability reporting channel, SLA targets, and supported versions.
   - Why: Align with OSS security best practices and package consumer expectations.

### Should have (important, medium-term)

1. **[Feature] Add automated dependency update workflow**
   - Prefer Dependabot or Renovate for NuGet/npm/GitHub Actions updates.
   - Why: Reduces patch lag and security exposure.

2. **[Issue] Resolve provider TODOs around event rehydration semantics**
   - Files include:
     - `src/src/EventSourcing.AzureStorage/AzureStorage/TableEventStore.GetAsync.cs`
     - `src/src/EventSourcing.MongoDB/MongoDB/Events/MongoDBEventStore.GetAsync.cs`
   - Why: Comments indicate known correctness uncertainty in version handling path.

3. **[Task] Tighten options validation for MongoDB store config**
   - File: `src/src/EventSourcing.MongoDB/MongoDB/Events/MongoDBEventStoreOptions.cs` has TODO regex comments for key names.
   - Why: Prevent runtime misconfiguration and harden input constraints.

4. **[Task] Add architecture decision records (ADRs)**
   - Capture transaction model, upcasting strategy, snapshot semantics, and compatibility guarantees.
   - Why: Improves maintainability as contributor count grows.

### Could have (valuable, lower urgency)

1. **[Feature] Add SBOM and artifact attestations in release flow**
   - e.g., CycloneDX SBOM generation and provenance attestation.
   - Why: Supply-chain transparency and enterprise readiness.

2. **[Feature] Publish benchmark trend reports**
   - Use existing performance test projects to produce periodic trend artifacts.
   - Why: Evidence-based performance regressions monitoring.

3. **[Task] Add issue/PR templates and triage labels**
   - Why: Improve contribution quality and maintainer throughput.

4. **[Feature] Add API compatibility checks in CI**
   - e.g., public API baseline checks between releases.
   - Why: Protect package consumers from accidental breaking changes.

### Won’t have now (defer intentionally)

1. **[Task] Multi-cloud deployment automation beyond current package release scope**
   - Not required for current NuGet-focused distribution model.

2. **[Feature] Full monorepo orchestration tooling migration**
   - Current tooling (`Justfile` + reusable workflows + central package mgmt) is sufficient.

## Prioritized delivery plan (practical sequence)

1. Governance/security docs (`SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CODEOWNERS`)
2. Workflow lint/warning cleanup (`release.yml` context warning)
3. Warning cleanup in sample value objects (CS9113)
4. TODO resolution in provider get/rehydration path + tests
5. Dependency automation (Dependabot/Renovate)
6. Optional enhancements (SBOM, API compat, benchmark trend publishing)

## Risk register snapshot

- **Low immediate delivery risk**: CI/release/test posture is strong.
- **Medium ecosystem risk**: Missing explicit security/disclosure and contribution governance docs.
- **Medium technical debt risk**: Known TODOs in provider logic could become reliability issues if left untracked.

## Overall assessment

**Verdict: Strong project with production-ready engineering patterns; primary improvements are governance hardening, dependency automation, and cleanup of known TODO/warning debt.**

If you want, the next step can be a concrete implementation PR plan that converts each MoSCoW item into tracked GitHub issues with acceptance criteria and estimates.
