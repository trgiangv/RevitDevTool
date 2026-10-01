# Execution Plan: MSTest Host Provider

Date: 2026-09-28

## Status

Active

## Outcome

`TestingFramework=mstest` discovers in the testhost and executes inside the
host through the existing `testing/run` path. The in-host engine is MSTest
4.4.1 on the assembly the generation already loaded. NUnit and TUnit keep
their current behavior.

## Context

Decision: [0038](../../decisions/0038-mstest-host-provider.md).

Kernel to extend, not replace:

- `FrameworkProvider` + `TestingRuntimeSessionManager`
- `AddTestingHostServices()` (Revit and AutoCAD both call it)
- `TestFrameworkId`, adapter `TestingFramework` map, MTP sibling pack
- TUnit's isolation plan (`Isolated`, net48 `WithDistinctFileIdentity`)

Proof is `tests/DevTools.MSTest.Runtime.Tests`, created with the runtime.
It covers exact `LoadFile` identity, later registration wins,
`CaptureTraceOutput=false` leaving Console/Trace unchanged, selection, and
cancel. `Logger.OnLogMessage` growing per runner is asserted there too.
There is no spike project.

[0038](../../decisions/0038-mstest-host-provider.md) locks MSTest 4.4.1 and
Microsoft.Testing.Platform 2.4.1 for runtime, that test project, the MTP
sibling, and the generation payload. Central `Microsoft.Testing.Platform`
stays 2.4.1 for NUnit, TUnit, and MSTest. Do not use 4.5
preview. Do not use 4.5 preview. In-repo `MSTest.Sdk` is 4.4.1, the same
patch as the host packages ([0035](../../decisions/0035-mstest-sdk-repo-tests.md)).

## Scope

In scope:

- `DevTools.MSTest.Runtime` session: register, `TestApplication`, map nodes.
- `tests/DevTools.MSTest.Runtime.Tests` on MSTest 4.4.1 and MTP 2.4.1:
  identity, generation, output, selection, cancel. This is the only MSTest
  proof project.
- `DevTools.Testing.Host/MSTest` policy, factory, provider.
- `DevTools.MSTest.MTP` discoverer (`--list-tests`) and builder hook.
- Adapter props/targets row, pack script, `TestFrameworkId.MSTest`.
- Architecture and product doc touch in the same change that lands the provider.

Out of scope:

- Removing or rewriting TUnit.
- Making `mstest` the default `TestingFramework`.
- `DevTools.MTP.Embedded`, a shared session base, or a custom MSTest catalog.
- `FrameworkFilter` / NUnit XML.
- New AppDomain, Revit-only scheduler, or a second trace-listener scope.
- Live matrix across every host year. One Revit year, two generations, is the
  live gate.

## Approach

### Wiring

```text
dotnet test / Test Explorer
        │
        ▼
DevTools.MSTest.MTP          short-lived testhost
  --list-tests                Register + TestApplication, no test bodies
  ITestDiscoverer
  Names → TestIds
        │
        ▼
DevTools.TestRunner           unchanged
        │
        ▼
testing/run
        │
        ▼
FrameworkProvider (MSTest)
        │
        ├─ MSTestGenerationPolicy     copy closure, MSTest 4.4.1 + MTP 2.4.1
        └─ ManifestRuntimeSessionFactory
                 │
                 ▼
           generation ALC / net48 distinct file
                 │
                 ▼
           MSTestRuntimeSession
                 Register(exact assembly, empty metadata)
                 testconfig: parallelism off, captureTrace false
                 AddMSTest → RunAsync
                 TestNodeUpdateMessage → TestCaseResult
```

`IHostContextExecutor` already runs `testing/run` on the host thread. MSTest
parallelism is turned off so the engine does not add workers under that call.

### Projects and types

| Piece | Path | Owns |
|-------|------|------|
| Runtime | `source/DevTools.MSTest.Runtime/` | `MSTestRuntimeSession`, `MSTestAssemblyRegistration`, `MSTestNodeResults`, payload targets |
| In-host provider | `source/DevTools.Testing.Host/MSTest/` | `MSTestGenerationPolicy` |
| Testhost sibling | `source/DevTools.MSTest.MTP/` | `MSTestTestDiscoverer`, builder hook |
| Tests | `tests/DevTools.MSTest.Runtime.Tests/` | identity, generation, output, selection, cancel |

`MSTestRuntimeSession` implements `ITestingRuntimeSession`. Copy the run gate
and cancel handshake from `TUnitRuntimeSession`. Do not extract a base class.

`MSTestAssemblyRegistration` is the only caller of
`ReflectionMetadataHook.Register`. Arguments are the kernel's `Assembly`, an
empty `Type[]`, and an empty method map. Call it once per session, before
`BuildAsync`.

`MSTestNodeResults` maps `PassedTestNodeStateProperty`,
`FailedTestNodeStateProperty`, and skipped/cancelled node states onto
`TestCaseResult`. It does not reference MSTest result types.

`MSTestGenerationPolicy` follows `TUnitGenerationPolicy`: scan the test output
directory, require the pinned framework DLL, copy `DevTools.MSTest.Runtime.dll`
and the runtime payload beside it. Payload targets copy local MSTest + MTP
the way `TUnitRuntimePayload.targets` copies TUnit.Core / Engine / MTP.

`ManifestRuntimeSessionFactory` activates the session by type name:
`AssemblyIsolationPlan` isolated, net48 distinct file identity, pin
`ITestingRuntimeSession`'s assembly, activate
`DevTools.MSTest.Runtime.MSTestRuntimeSession` with
`(testAssembly, shadowPath, generationId)`.

Session `Run` builds one `TestApplication` per call:

- `--config-file` with the two settings in 0038. No runsettings file.
- `All` passes no filter.
- `TestIds` pass `--filter-uid` with one MTP node uid per id.
- Empty `TestIds` returns an empty result without starting the application.
- `FrameworkFilter` returns `testing/invalid_request`.

Cancel calls the generation MTP cancellation source (0038 decision 8). Dispose
the application after `RunAsync`. Do not call `Environment.Exit`.

### Adapter and composition

- `TestFrameworkId.MSTest`.
- `AddMSTestHostServices()` from `AddTestingHostServices()`.
- `RevitDevTool.TestAdapter.props` / `.targets`: `mstest` →
  `DevTools.MSTest.MTP.dll` and the hook type. Same shape as the `tunit` row.
- `scripts/pack-test-adapter.ps1` builds and packs the new sibling next to
  NUnit.MTP and TUnit.MTP.
- Host installer copy of `MSTestRuntime` follows the TUnit runtime folder.

### What this change does not add

No edit to `TestingRuntimeSessionManager`, `TestingGenerationStore`, or
`TestingRequestHandler` beyond the new provider registration. No MSTest
type in `DevTools.Testing.Abstractions` except the enum value. No change to
NUnit or TUnit runtime.

## Risks And Recovery

- **4.4.1 moves `Register` or `--list-tests`.** `DevTools.MSTest.Runtime.Tests`
  is the gate. Do not wire the host provider until identity, generation, and
  output pass on 4.4.1 / 2.4.1. A signature change is a plan update, not a
  silent pin drop.
- **`TestApplication` executes during `--list-tests`.** Discovery must not
  increment the fixture probe. If it does, stop and fix list mode before
  packing a sibling.
- **Environment-variable controller relaunches Revit.** Runsettings omit that
  section. The live gate asserts the host PID is unchanged.
- **`RunAsync` calls `Environment.Exit`.** The runtime test process must
  return. The live gate repeats that assertion inside Revit. If it exits the
  host, Route A stops; do not switch to `MSTestEngine` reflection under that
  failure without a new decision.
- **Cancel token is ignored.** Runtime test cancels a sleeping `[TestMethod]`
  and expects a cancelled `TestCaseResult`. If the internal cancel source
  does not reach MSTest, that test is the blocker, not a reason to reflect
  `MSTestEngine`.
- **Rollback.** The provider is additive. Revert the `mstest` map row and
  `AddMSTestHostServices()` to return the host to NUnit/TUnit only.

## Progress

- [x] Add `DevTools.MSTest.Runtime` and `tests/DevTools.MSTest.Runtime.Tests` together, pinned to MSTest 4.4.1 and MTP 2.4.1.
- [x] Cover identity, generation, output, selection (`All`, empty `TestIds`, one `TestId`), and cancel in that test project. Host wiring waits on those tests.
- [x] Add `Testing.Host/MSTest` policy, factory, provider, and `TestFrameworkId.MSTest`.
- [x] Add `DevTools.MSTest.MTP` list-tests discoverer and builder hook. Assert discover does not run test bodies.
- [x] Register the provider and the adapter/pack row.
- [x] Update `docs/architecture/Testing/README.md` and `docs/product/host-testing.md`.
- [ ] Live gate: one Revit year, two generations of the same assembly name, PID unchanged, generation B is the one that ran.

## Decisions

- 2026-09-28: New flow pin is MSTest 4.4.1 and Microsoft.Testing.Platform 2.4.1 together. Not MTP 2.4.0, not 4.5 preview. Recorded in [0038](../../decisions/0038-mstest-host-provider.md).
- 2026-09-28: No spike harness. `tests/DevTools.MSTest.Runtime.Tests` is the proof layer and is added with the runtime.
- 2026-09-29: In-repo `MSTest.Sdk` is 4.4.1. Central Microsoft.Testing.Platform is 2.4.1 for NUnit, TUnit, and MSTest.
- 2026-09-28: First gate passed. `dotnet run --project tests/DevTools.MSTest.Runtime.Tests/DevTools.MSTest.Runtime.Tests.csproj -c Debug -f net48` exited 0. Identity, generation, output, selection, and cancel each printed PASS.
- 2026-09-29: Host provider registered. `DevTools.Testing.Host.Tests` 67 passed. MTP `--list-tests` did not execute the probe (`tests/DevTools.MSTest.MTP.Tests` PASS). Adapter filter `TestingFrameworkMapTests|AdapterArchitectureTests|PackageConsumerTests` 25 passed. Architecture and product docs updated. Live Revit two-generation gate still open.

## Validation

- Focused proof:

  ```powershell
  dotnet build source/DevTools.MSTest.Runtime/DevTools.MSTest.Runtime.csproj -c Debug
  dotnet run --project tests/DevTools.MSTest.Runtime.Tests/DevTools.MSTest.Runtime.Tests.csproj
  dotnet build source/DevTools.Testing.Host/DevTools.Testing.Host.csproj -c Debug
  dotnet build source/DevTools.MSTest.MTP/DevTools.MSTest.MTP.csproj -c Debug
  ```

  Runtime tests must show `ReferenceEquals` for a `LoadFile` assembly, generation B winning when A's path is requested, stable `Console`/`Trace` with capture off, and cancel of an in-flight test.

- Integration or end-to-end proof: existing NUnit and TUnit host test projects still discover and run. One MSTest sample with `TestingFramework=mstest` lists tests in the testhost and runs them through TestRunner. Live Revit: generation B executes, host PID unchanged.
- Repository-required checks: compile the touched csprojs from the build skill. Adapter pack script is dry-run only if the pack layout changes; do not publish.

## Result

Pending.
