---
name: dotnet-tunit
description: Use .NET with TUnit-specific test conventions.
category: dotnet
roles:
  - dotnet
  - dotnet-tunit
  - coding
tags:
  - dotnet
  - csharp
  - tunit
  - tests
---

# dotnet-tunit Skill

Use this skill for .NET repositories that use TUnit.

## Rules

- Use `TUnit.Core`.
- Use `TUnit.Assertions`.
- Test methods must have `[Test]`.
- Assertion calls must be awaited.
- Prefer `await Assert.That(actual).IsEqualTo(expected);`.
- Do not use xUnit, NUnit, MSTest, or FluentAssertions unless explicitly requested.
- Run the relevant `dotnet test` command before completion.
