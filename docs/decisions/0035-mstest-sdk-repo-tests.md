# 0035 In-Repo Tests Use MSTest.Sdk And First-Party Coverage

Date: 2026-09-13

## Status

Accepted

## Context

[0034](0034-execution-mstest-sdk-scoped-tests.md) moved Execution tests to
MSTest.Sdk 4.4.0 and Microsoft Code Coverage (`--coverage`). It left the rest
of `tests/*.Tests` on xUnit v3 + `coverlet.MTP`.

That split is no longer the desired end state. Two harnesses and two collectors
in one repo keep Coverlet testhost locks, mixed assert APIs, and a
`coverlet.MTP` feed that 0034 already rejected for Execution. `global.json`
already pins `MSTest.Sdk` **4.4.0**. `tests/DevTools.TestRunner.Tests` already
runs on that SDK.

0022 keeps repository tests on Microsoft Testing Platform. It does not require
xUnit.

## Decision

Every in-repo `tests/*.Tests` executable uses **MSTest.Sdk 4.4.0** (version
from `global.json`) and does **not** reference `xunit.v3` or `coverlet.MTP`.

### Coverage

| Testhost | Collector |
|----------|-----------|
| net8+ / net10 (`UseMicrosoftCodeCoverage=true`) | MSTest.Sdk **Default** profile → `Microsoft.Testing.Extensions.CodeCoverage`. Run `--coverage --coverage-output-format cobertura --coverage-settings coverage.xml`. |
| net48 (`*.NetFramework.Tests`) | No collector. `TestingExtensionsProfile=None`. `coverlet.MTP` was already net8-only. |

Do not enable unmanaged native instrumentation
(`EnableStaticNativeInstrumentation` / `EnableDynamicNativeInstrumentation`).
Do not add Coverlet next to Microsoft coverage.

`tests/DevTools.TestRunner.Tests` drops `TestingExtensionsProfile=None` and
opts into the same Microsoft collector as other net10 testhosts.

### Style

Same as Execution / TestRunner: `[TestClass]`, `[TestMethod]`, `[DataRow]`,
instance `TestContext`, `Assert.Inconclusive` for runtime skip.

Optional samples stay as they are: `samples/ricaun.NUnit.SampleTests` is VSTest
comparison; `samples/DevTools.*.SampleTests` are in-host NUnit/TUnit, not this
harness.

## Alternatives Considered

1. **Keep xUnit + Coverlet outside Execution.** Rejected — two collectors and
   Coverlet `bin/` locks remain.
2. **VSTest + `Microsoft.NET.Test.Sdk`.** Rejected by 0022 (MTP-only).
3. **Microsoft coverage on net48 testhosts.** Not required; Coverlet never
   covered them.

## Consequences

Positive:

- One unit-test SDK and one coverage flag (`--coverage`) for out-of-host
  `tests/`.
- Coverlet testhost locks go away with the package.

Tradeoffs:

- net48 testhosts have no line-coverage collector.
- Conversion is mechanical but large (assert API, collections →
  `DoNotParallelize`).

## Follow-Up

- Implemented: `docs/plans/completed/2026-09-13-mstest-sdk-repo-migration.md`.
- `xunit.v3` and `coverlet.MTP` removed from `Directory.Packages.props`.
  `tests/Directory.Build.props` no longer injects Coverlet.
