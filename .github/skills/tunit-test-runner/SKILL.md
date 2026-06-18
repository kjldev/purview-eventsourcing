---
name: tunit-test-runner
description: >-
  Run, filter, and select TUnit tests through `dotnet test`. Use this whenever
  executing .NET tests in a project that depends on TUnit (built on
  Microsoft.Testing.Platform / MTP, not VSTest), when `dotnet test --filter`
  reports "Zero tests ran", or when tests must be narrowed by assembly,
  namespace, class, test name, [Category], or other custom properties. Covers
  the `--treenode-filter` path-based query syntax, its operators, the `--`
  separator rules across SDK versions, and common 0-tests troubleshooting.
license: MIT
---

# Running TUnit tests with `dotnet test`

## The one rule that matters most

TUnit runs on **Microsoft.Testing.Platform (MTP)**, not VSTest. The reflexive
`dotnet test --filter "Category=X"` **does not work**: MTP silently rejects the
`--filter` flag, prints its own help text, and exits with `Zero tests ran`. That
looks like a passing-but-empty run or a config failure, but it is just an
unrecognised flag. **Never reach for `--filter` on a TUnit project.** Use
`--treenode-filter` instead.

| Other frameworks (VSTest) | TUnit (MTP) |
| --- | --- |
| `--filter "Category=Integration"` | `--treenode-filter "/*/*/*/*[Category=Integration]"` |
| `--filter "FullyQualifiedName~LoginTests"` | `--treenode-filter "/*/*/LoginTests/*"` |
| `--filter "Name=AcceptCookiesTest"` | `--treenode-filter "/*/*/*/AcceptCookiesTest"` |

## How to invoke it

Prefer `dotnet test` over `dotnet run`: `dotnet test` builds and runs every
targeted TFM automatically and works against a `.csproj`, `.sln`, or `.slnx`,
whereas `dotnet run` only runs a single TFM.

The catch is the `--` separator, which depends on the SDK:

```bash
# Universal form — works on every SDK. Use this by default.
dotnet test -- --treenode-filter "/*/*/LoginTests/*"

# .NET 10+ SDK only — the platform flag can be passed directly.
dotnet test --treenode-filter "/*/*/LoginTests/*"
```

Anything after `--` is passed through to the TUnit test runner rather than to
the `dotnet test` command itself. Flags from extension packages
(`--coverage`, `--report-trx`, `--results-directory`, etc.) **must** also sit
after the `--`:

```bash
dotnet test --configuration Release --no-build \
  -- --treenode-filter "/*/*/*/*[Category=Unit]" --coverage --report-trx
```

> Run with no filter to execute everything: `dotnet test`.

## The `--treenode-filter` syntax

A filter is a path with four segments, optionally annotated with a property
group on any segment:

```
/<Assembly>/<Namespace>/<Class>/<TestName>[Property=Value]
```

Use `*` as a wildcard in any segment. The classic "run all tests" filter is
`/*/*/*/*` — four wildcards, one per level.

### Operators

| Operator | Meaning | Example |
| --- | --- | --- |
| `*` | Wildcard within a segment | `/*/*/LoginTests*/*` |
| `=` | Property equals (exact) | `/*/*/*/*[Category=Unit]` |
| `!=` | Property not equal (exclude) | `/*/*/*/*[Category!=Slow]` |
| `&` | AND — within one segment / property group | `/**[(Category=Unit)&(Priority=High)]` |
| `\|` | OR — within one segment / property group | `/*/*/(LoginTests)\|(SignupTests)/*` |
| `**` | Match any path depth (must be at the **end**) | `/MyAssembly/**` |

### Two grammar rules that are easy to get wrong

1. **`&` and `|` operate *inside a single segment or property group*, and each
   side must be wrapped in parentheses.** They do not join two complete paths.
   - AND across class patterns: `/*/*/(ClassA*)&(*Smoke)/*`
   - OR across classes: `/*/*/(Class1)|(Class2)/*`
   - AND across properties: `/**[(Category=Unit)&(Priority=High)]`
   - OR across properties: `/**[(Category=Smoke)|(Priority=High)]`

2. **Only one property group `[...]` is allowed per path segment.** Combine
   conditions *inside* the single bracket — do not chain brackets.
   - Correct: `/*/*/*/*[(A=1)&(B=2)]`
   - Wrong: `/*/*/*/*[A=1][B=2]` and `/*/*/*/*[A=1]&[B=2]`

3. **`**` must terminate the path.** `/MyAssembly/**` is valid; `/**/Class/*`
   is not.

## Recipe reference

```bash
# Everything
dotnet test

# One class
dotnet test -- --treenode-filter "/*/*/LoginTests/*"

# One test method by name
dotnet test -- --treenode-filter "/*/*/*/AcceptCookiesTest"

# Whole namespace
dotnet test -- --treenode-filter "/*/MyProject.Tests.Integration/*/*"

# Namespace prefix (wildcard)
dotnet test -- --treenode-filter "/*/MyProject.Tests.Api*/*/*"

# By [Category]
dotnet test -- --treenode-filter "/*/*/*/*[Category=Smoke]"

# Partial property value (wildcard)
dotnet test -- --treenode-filter "/*/*/*/*[Owner=*Team-Backend*]"

# Exclude a category
dotnet test -- --treenode-filter "/*/*/*/*[Category!=Slow]"

# AND two properties (single bracket)
dotnet test -- --treenode-filter "/*/*/*/*[(Category=Smoke)&(Priority=High)]"

# OR two classes
dotnet test -- --treenode-filter "/*/*/(LoginTests)|(SignupTests)/*"

# OR two properties (single bracket)
dotnet test -- --treenode-filter "/**[(Category=Smoke)|(Priority=High)]"

# Namespace + property combined
dotnet test -- --treenode-filter "/*/MyProject.Tests.Integration/*/*[Priority=Critical]"

# Exclude a whole class folder (CI: skip smoke tests)
dotnet test -- --treenode-filter "/*/(!*SmokeTests)/*/*"
```

To preview what a filter selects without running anything, list tests first:

```bash
dotnet test -- --list-tests
```

## `[Explicit]` tests

Tests (or whole classes) marked `[Explicit]` only run when **every** test
matched by the filter is explicit. Target them precisely — by name, by class,
or by a `[Category]` you reserve for them:

```bash
dotnet test -- --treenode-filter "/*/*/*/*[Category=DevTool]"
```

If a filter matches a mix of explicit and non-explicit tests, the explicit ones
are excluded from that run.

## Troubleshooting: "Zero tests ran" / 0 tests discovered

Check, in order:

1. **`--filter` was used instead of `--treenode-filter`.** This is the most
   common cause. Switch the flag.
2. **`Microsoft.NET.Test.Sdk` is still referenced.** It conflicts with the
   TUnit MTP platform — remove the `<PackageReference Include="Microsoft.NET.Test.Sdk" />`.
3. **TUnit package missing.** Ensure `<PackageReference Include="TUnit" Version="*" />`.
4. **Missing `[Test]` attribute**, or the test method is not a `public`
   instance method (non-public/static methods are not discovered).
5. **Wrong `OutputType`.** A `hostfxr.dll could not be found` error means the
   project needs `<OutputType>Exe</OutputType>`.
6. **Bad filter shape.** Remember the path has exactly four segments; a missing
   wildcard (e.g. `/*/*/LoginTests` instead of `/*/*/LoginTests/*`) matches
   nothing.
7. **IDE only:** MTP/"Testing Platform support" must be enabled (VS: Preview
   Features → "Use testing platform server mode"; Rider: Unit Testing →
   Testing Platform support; VS Code: install C# Dev Kit), then rebuild.

## References

- TUnit — Test Filters: https://tunit.dev/docs/execution/test-filters/
- TUnit — Troubleshooting & FAQ: https://tunit.dev/docs/troubleshooting/
- TUnit — CI/CD pipelines: https://tunit.dev/docs/examples/tunit-ci-pipeline/
- TUnit — Explicit tests: https://tunit.dev/docs/writing-tests/explicit/
- MTP graph-query (tree-node) filtering spec: https://github.com/microsoft/testfx/blob/main/docs/mstest-runner-graphqueryfiltering/graph-query-filtering.md
