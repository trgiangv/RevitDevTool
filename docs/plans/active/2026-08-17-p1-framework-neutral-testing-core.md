# P1 Framework-Neutral Testing Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:subagent-driven-development` (recommended) or
> `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the host-test control plane from `DevTools.NUnit.*`, rename
the executable directly to `DevTools.TestRunner`, and preserve NUnit behavior
through a thin provider over framework-neutral contracts.

**Architecture:** `DevTools.Testing.Abstractions` defines dependency-free wire
and provider contracts; `DevTools.Testing.Transport` implements JSON/IPC;
`DevTools.Testing.Mtp` maps neutral events to MTP; and
`DevTools.Testing.Host` dispatches requests to framework providers. NUnit keeps
all NUnit discovery/runtime semantics in `DevTools.NUnit.*`.

**Tech Stack:** C# multi-targeting (`net48`, `net8.0-windows`,
`net10.0-windows`), Microsoft Testing Platform 2.3.3, Named Pipes,
System.Text.Json, NUnit 4.6.1, Autodesk host composition.

## Global Constraints

- Decision: [0020 Framework-Neutral MTP Host Testing](../../decisions/0020-framework-neutral-mtp-host-testing.md).
- P0 xUnit 4 repository baseline must be complete before this plan starts.
- Discovery never locates, launches, attaches to, or opens an Autodesk host.
- Only execution may start `DevTools.TestRunner` and activate a host.
- `DevTools.Testing.Abstractions` contains no MTP, NUnit, xUnit, Autodesk,
  reflection-discovery, JSON, process, or IPC dependency.
- Preserve full NUnit 4.6.1 semantics and the existing private runtime closure.
- Rename directly to `DevTools.TestRunner.exe`; do not ship a
  `DevTools.NUnit.Runner.exe` alias.
- Keep the NUnit VSTest-only surface until a separate removal decision.
- Do not commit unless the user explicitly requests a commit.

---

Date: 2026-08-17

## Priority And Dependencies

- Priority: **P1**.
- Depends on: [P0 xUnit 4 Repository MTP Baseline](2026-08-17-p0-xunit4-repository-mtp-baseline.md).
- Unblocks: P2 xUnit 4 host provider.
- Supersedes after parity: remaining framework-neutral work in
  [2026-08-12 NUnit Native Runtime And MTP-First Integration](2026-08-12-nunit-native-runtime-mtp.md).

## Status

Active — P0 CLI exit gate is complete. Tasks 1-7 landed; remaining work is host composition, packaging, and live NUnit parity.

## Harness Execution Rules

- Resume this file as the only plan for the P1 workstream and update progress,
  task-local decisions, evidence, and recovery notes after every task.
- Read `.agents/skills/platform-change/SKILL.md` before source edits and
  `.agents/skills/build/SKILL.md` before compile/deploy proof.
- Preserve unrelated dirty work; do not delete or rewrite predecessor plans
  until their remaining work has been explicitly superseded.
- Only one worker may own the live-host lane. Record host PID before deployment
  or test execution and account for it before starting another host.
- Use compile-only host builds unless the live task explicitly requires deploy;
  kill the selected host through `scripts/kill-host.ps1` before deploying.
- Stop on protocol, packaging, ALC, or live-host failures and record exact
  evidence. Do not turn a failed gate into a silent compatibility fallback.
- Keep all work uncommitted until the user explicitly authorizes a commit.

## Outcome

There is one generic TestRunner executable, one generic host protocol, and one
generic MTP control layer. NUnit discovery remains local; NUnit execution still
runs inside Revit/AutoCAD with the same runtime lifecycle, generation isolation,
debug attach, cancellation, output, and result semantics. No shared testing
project references NUnit or xUnit.

## Context

- Decision: [0020](../../decisions/0020-framework-neutral-mtp-host-testing.md).
- Existing protocol/contracts: `source/DevTools.NUnit.Core`.
- Existing serialization/IPC: `source/DevTools.NUnit.Transport`.
- Existing host dispatch/generation: `source/DevTools.NUnit.Host`.
- Existing MTP provider: `source/DevTools.NUnit.Mtp`.
- Existing runner: `source/DevTools.NUnit.Runner`.
- Existing Visual Studio debug work:
  [2026-08-15](2026-08-15-nunit-visual-studio-debug.md).
- Boundary policy: `docs/agents/host-boundaries.md` and ADR 0019.

## Scope

In scope:

- New Abstractions, Transport, MTP, and Host projects under `source/DevTools.Testing.*`.
- Generic `testing/*` protocol and provider registry.
- Direct project/assembly/executable rename to `DevTools.TestRunner`.
- Extraction of generation staging primitives reusable by NUnit and xUnit.
- NUnit provider migration with unchanged NUnit framework semantics.
- NUnit MTP and VSTest-only callers moved to the generic Runner contracts.
- Revit and AutoCAD host composition.
- Architecture, unit, packaging, and live NUnit parity gates.

Out of scope:

- Implementing the xUnit provider.
- Removing the NUnit VSTest-only projects.
- Changing NUnit version or test semantics.
- Adding a universal framework discoverer.
- Launching a host during discovery.
- Automatic force termination of in-process tests.
- TUnit integration.

## Target Project And File Map

| Project / file | Responsibility |
|---|---|
| `source/DevTools.Testing.Abstractions/` | Neutral DTOs, framework IDs, provider and event-sink contracts. |
| `source/DevTools.Testing.Transport/` | JSON source generation, Named Pipe envelopes, process TestRunner client. |
| `source/DevTools.Testing.Mtp/` | Shared MTP session lifecycle and neutral event-to-node helpers. |
| `source/DevTools.Testing.Host/` | Provider registry, generic request handler, generation staging. |
| `source/DevTools.TestRunner/` | CLI, host locate/launch/reuse, debugger attach, generic pipe client. |
| `source/DevTools.NUnit.Core/` | NUnit-only discovery/runtime contracts and mapping data. |
| `source/DevTools.NUnit.Host/` | NUnit provider registration and runtime adapter. |
| `source/DevTools.NUnit.Mtp/` | NUnit local discovery and NUnit-specific MTP mapping. |
| `source/DevTools.NUnit.Runtime/` | Authoritative NUnit runtime; still the only production NUnit reference. |
| `source/DevTools.NUnit.TestAdapter/` | Retained VSTest compatibility caller of generic TestRunner. |

## Neutral Interfaces

Task 1 introduces these exact public contracts in
`DevTools.Testing.Abstractions`; later tasks consume them without redefining
parallel DTOs:

```csharp
public static class TestingFrameworkIds
{
    public const string NUnit = "nunit";
    public const string Xunit = "xunit";
}

public sealed record TestingHostOptions(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath,
    int? DebugParentPid = null);

public sealed record TestingAssemblyReference(
    string Path,
    string? TargetFramework,
    string? ContentHash);

public sealed record TestingSelection(
    IReadOnlyList<string> TestIds,
    string? ProviderPayload = null);

public sealed record TestingRunRequest(
    int ProtocolVersion,
    Guid RunId,
    string FrameworkId,
    TestingAssemblyReference Assembly,
    TestingSelection Selection,
    IReadOnlyDictionary<string, string> FrameworkOptions);

public sealed record TestingAttachment(string Path, string? Description);
public sealed record TestingSourceLocation(string File, int Line);
public sealed record TestingTrait(string Name, string Value);

public sealed record TestingCaseResult(
    string TestId,
    string DisplayName,
    string Outcome,
    double DurationMilliseconds,
    string? Message,
    string? StackTrace,
    string? Output,
    TestingSourceLocation? Source,
    IReadOnlyList<TestingTrait> Traits,
    IReadOnlyList<TestingAttachment> Attachments);

public enum TestingCancellationState
{
    None,
    Requested,
    Acknowledged,
    Completed,
    Poisoned,
}

public static class TestingEventKinds
{
    public const string Case = "case";
    public const string Output = "output";
    public const string Attachment = "attachment";
    public const string Diagnostic = "diagnostic";
    public const string Cancellation = "cancellation";
}

public sealed record TestingEvent(
    Guid RunId,
    string Kind,
    TestingCaseResult? Case,
    string? Message,
    TestingAttachment? Attachment,
    TestingCancellationState CancellationState);

public sealed record TestingRunResponse(
    Guid RunId,
    string FrameworkId,
    string? GenerationId,
    IReadOnlyList<TestingCaseResult> Results,
    TestingCancellationState CancellationState,
    string? DiagnosticCode,
    string? DiagnosticMessage);

public interface ITestingEventSink
{
    void Publish(TestingEvent testingEvent);
}

public interface IHostTestFrameworkProvider
{
    string FrameworkId { get; }
    TestingRunResponse Run(
        TestingRunRequest request,
        ITestingEventSink eventSink,
        CancellationToken cancellationToken);
    bool Cancel(Guid runId);
}
```

The wire envelope adds `testing/hello`, `testing/run`, `testing/cancel`, and
`testing/progress`. There is intentionally no host `testing/discover` endpoint.

## Approach

### Task 1: Establish dependency-free neutral contracts

**Files:**

- Create: `source/DevTools.Testing.Abstractions/DevTools.Testing.Abstractions.csproj`
- Create: `source/DevTools.Testing.Abstractions/Contracts/TestingContracts.cs`
- Create: `source/DevTools.Testing.Abstractions/Providers/IHostTestFrameworkProvider.cs`
- Create: `tests/DevTools.Testing.Abstractions.Tests/DevTools.Testing.Abstractions.Tests.csproj`
- Create: `tests/DevTools.Testing.Abstractions.Tests/AssemblyBoundaryTests.cs`
- Create: `tests/DevTools.Testing.Abstractions.Tests/ContractRoundTripTests.cs`
- Modify: `RevitDevTool.slnx`

**Interfaces:** Produces every type in “Neutral Interfaces”.

- [ ] Write boundary tests that load the built assembly references and reject
  `Microsoft.Testing.*`, `NUnit*`, `xunit*`, `Autodesk*`, `System.Text.Json`,
  `DevTools.Ipc`, and `System.Diagnostics.Process` dependencies.
- [ ] Write equality/opaque-ID tests proving whitespace, punctuation, and
  framework-specific IDs round-trip without FQN normalization. Add contract
  round-trip cases for every cancellation state.
- [ ] Add the multi-target project and exact contracts above. Use no
  implementation package references.
- [ ] Build all three TFMs and run the focused tests:

```powershell
dotnet build source/DevTools.Testing.Abstractions/DevTools.Testing.Abstractions.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.Testing.Abstractions.Tests/DevTools.Testing.Abstractions.Tests.csproj
```

Expected: boundary and opaque-identity tests pass for a dependency-free DLL.

### Task 2: Extract generic transport and protocol ownership

**Files:**

- Create: `source/DevTools.Testing.Transport/DevTools.Testing.Transport.csproj`
- Move/adapt: `source/DevTools.NUnit.Transport/NUnitJsonContext.cs`
- Move/adapt: `source/DevTools.NUnit.Transport/NUnitProtocolBridge.cs`
- Create: `source/DevTools.Testing.Transport/TestingJsonContext.cs`
- Create: `source/DevTools.Testing.Transport/TestingProtocolBridge.cs`
- Create: `source/DevTools.Testing.Transport/ITestRunnerTransport.cs`
- Create: `source/DevTools.Testing.Transport/ProcessTestRunnerClient.cs`
- Create: `tests/DevTools.Testing.Transport.Tests/`

**Interfaces:**

- Consumes: `TestingRunRequest`, `TestingRunResponse`, and
  `TestingHostOptions`.
- Produces:

```csharp
public interface ITestRunnerTransport : IDisposable
{
    TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult> onResult);
    void Cancel(Guid runId);
}
```

- [ ] Add golden JSON tests for every neutral request/result/event field,
  including assembly identity and all cancellation states, plus protocol
  mismatch tests for version `1` versus the new current version.
- [ ] Add process-client tests with a fake executable that captures arguments;
  assert discovery code paths never instantiate this client.
- [ ] Implement JSON source generation and IPC envelopes in Transport, not
  Abstractions.
- [ ] Keep a temporary NUnit legacy serializer bridge only for existing
  `nunit/*` requests; mark it internal and prevent new fields from being added.
- [ ] Run Transport tests and Core golden tests before removing
  `DevTools.NUnit.Transport` from consumers.

### Task 3: Rename the executable directly to DevTools.TestRunner

**Files:**

- Move: `source/DevTools.NUnit.Runner/` to `source/DevTools.TestRunner/`
- Move: `tests/DevTools.NUnit.Runner.Tests/` to `tests/DevTools.TestRunner.Tests/`
- Modify: `build/Modules/PublishNUnitRunnerModule.cs` and all module registrations
- Modify: `source/DevTools.NUnit.Mtp/build/RevitDevTool.NUnit.targets`
- Modify: `source/DevTools.NUnit.Core/Client/NUnitRunnerPaths.cs`
- Modify: solution, scripts, packaging, installer, and documentation references

**Interfaces:**

- Consumes: `ITestRunnerTransport` and neutral contracts.
- Produces: exactly one installed executable named `DevTools.TestRunner.exe`.

- [x] First update Runner path/assembly tests to require
  `DevTools.TestRunner.exe` and reject `DevTools.NUnit.Runner.exe` anywhere in
  bundle/package output.
- [x] Rename project, assembly, root namespace, commands, tests, build module,
  output paths, kill-before-publish target, and generated host settings in one
  coherent change.
- [x] Preserve `--debug` and `--debug-parent-pid` behavior from the active
  Visual Studio debug plan.
- [x] Keep framework-specific CLI parsing behind an explicit `--framework`
  value; default to NUnit only for the retained legacy NUnit call path.
- [x] Run:

```powershell
dotnet build source/DevTools.TestRunner/DevTools.TestRunner.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj
rg -n "DevTools\.NUnit\.Runner" source tests build samples docs/product docs/agents
```

Expected: build/tests pass and the search returns only historical decision/plan
text, never a deployed path or project reference.

### Task 4: Add generic host provider dispatch and generation staging

**Files:**

- Create: `source/DevTools.Testing.Host/DevTools.Testing.Host.csproj`
- Create: `source/DevTools.Testing.Host/TestingProviderRegistry.cs`
- Create: `source/DevTools.Testing.Host/TestingRequestHandler.cs`
- Create: `source/DevTools.Testing.Host/TestingCancellationStateMachine.cs`
- Create: `source/DevTools.Testing.Host/Loading/TestingGenerationBuilder.cs`
- Create: `source/DevTools.Testing.Host/Loading/TestingGenerationManifest.cs`
- Create: `tests/DevTools.Testing.Host.Tests/`

**Interfaces:**

- Consumes: `IHostTestFrameworkProvider` registrations.
- Produces:

```csharp
public sealed class TestingProviderRegistry
{
    public TestingProviderRegistry(IEnumerable<IHostTestFrameworkProvider> providers);
    public IHostTestFrameworkProvider GetRequired(string frameworkId);
}

public sealed record TestingRuntimePayload(
    string FrameworkId,
    string TestAssemblyPath,
    string RuntimeAssemblyPath,
    string FrameworkAssemblyPath,
    IReadOnlyList<string> AdditionalProbeRoots);
```

- [x] Write tests for duplicate/unknown framework IDs, case-stable IDs,
  cancellation routing and the ordered
  `Requested -> Acknowledged -> Completed|Poisoned` transition, provider
  exceptions, and poisoned-session responses.
- [x] Extract content hash, immutable snapshot copy, managed/native asset index,
  and generation manifest mechanics from NUnit Host. Keep provider-owned
  framework validation and load-context policy outside generic staging.
- [x] Implement `testing/*` handling and route legacy `nunit/*` envelopes to
  the NUnit provider through a compatibility adapter.
- [x] Assert there is no discovery endpoint and no host-locate/process API in
  Testing.Host.

### Task 5: Adapt NUnit Host/Runtime to the provider contract

**Files:**

- Modify: `source/DevTools.NUnit.Host/NUnitRuntimeManager.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitRequestHandler.cs`
- Create: `source/DevTools.NUnit.Host/NUnitHostTestFrameworkProvider.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/*`
- Modify: `source/DevTools.NUnit.Runtime/*`
- Modify: NUnit Core/Host/Runtime tests

**Interfaces:**

- Consumes: `IHostTestFrameworkProvider`, generic staging, neutral events.
- Produces: provider ID `TestingFrameworkIds.NUnit` with unchanged NUnit
  discovery/execution semantics.

- [x] Add parity tests that run the existing NUnit fixture matrix through the
  provider and compare IDs, outcomes, traits, source, output, attachments,
  cancellation, and generation ID with the pre-migration path.
- [x] Map neutral selection IDs to NUnit's existing authoritative IDs; never
  reconstruct them from display name.
- [x] Reuse current collectible ALC/net48 generation behavior and keep NUnit
  assemblies provider-private.
- [ ] Delete duplicated NUnit staging/control code only after parity tests pass.
- [x] Run Core, Runtime, Host, and net48 Host test projects with their existing
  focused commands.

### Task 6: Extract shared MTP orchestration and migrate NUnit MTP

**Files:**

- Create: `source/DevTools.Testing.Mtp/DevTools.Testing.Mtp.csproj`
- Create: `source/DevTools.Testing.Mtp/TestingMtpSession.cs`
- Create: `source/DevTools.Testing.Mtp/TestingNodeProperties.cs`
- Create: `source/DevTools.Testing.Mtp/TestingRunnerSession.cs`
- Modify: `source/DevTools.NUnit.Mtp/DevToolsNUnitFramework.cs`
- Modify: `source/DevTools.NUnit.Mtp/DevToolsNUnitSession.cs`
- Modify: `source/DevTools.NUnit.Mtp/TestingPlatformBuilderHook.cs`
- Modify: `tests/DevTools.NUnit.Mtp.Tests/*`

**Interfaces:**

- Consumes: MTP request/session types and neutral transport events.
- Produces reusable helpers only; `DevTools.Testing.Mtp` does not register a
  universal `ITestFramework`.

- [x] Add architecture tests rejecting NUnit/xUnit/Autodesk/process references
  from Testing.Mtp.
- [x] Extract session close/cancel, error node, timing, output, attachment,
  source, and trait mapping that is genuinely framework-neutral.
- [x] Keep NUnit metadata discovery, NUnit stable IDs, filter semantics, and
  NUnit builder-hook registration inside `DevTools.NUnit.Mtp`.
- [x] Add a no-host discovery test with a fake Runner path that throws if read;
  a `DiscoverTestExecutionRequest` must still complete.
- [x] Add a run test proving `RunTestExecutionRequest` starts the generic
  TestRunner transport with framework ID `nunit`.

### Task 7: Migrate retained VSTest callers without removing them

**Files:**

- Modify: `source/DevTools.NUnit.TestAdapter/*`
- Modify: `tests/DevTools.NUnit.TestAdapter.Tests/*`
- Modify: the four VSTest-only sample/test projects only where runner paths or
  contracts changed

- [x] Keep `Microsoft.TestPlatform.ObjectModel`, Test SDK, and NUnit adapter
  dependencies only in the approved VSTest surface.
- [x] Replace NUnit-named process transport/path types with
  `ITestRunnerTransport`, `TestingHostOptions`, and `DevTools.TestRunner.exe`.
- [x] Keep discovery local; add a test that VSTest discovery succeeds when the
  configured TestRunner and Autodesk executable paths do not exist.
- [x] Run TestAdapter tests and net48 VSTest discovery before proceeding.

### Task 8: Compose, package, and prove host parity

**Files:**

- Modify: `source/RevitDevTool/RevitDevTool.csproj` and composition registration
- Modify: `source/AcadDevTool/AcadDevTool.csproj` and composition registration
- Modify: `source/DevTools.NUnit.Host/build/*`
- Modify: build/installer modules and `RevitDevTool.slnx`
- Modify: `docs/architecture/<testing module>/` or the existing NUnit module
  document, but only one architecture layer
- Update: the two predecessor NUnit active plans after parity

- [ ] Register Testing.Host once and NUnit provider once in each Autodesk host.
- [ ] Package `DevTools.Testing.Abstractions` loose exactly once; keep NUnit
  runtime/framework assemblies in the NUnit runtime folder and generation.
- [ ] Add package-layout tests rejecting duplicate Abstractions, merged NUnit,
  old Runner executable, and missing provider runtime assets.
- [ ] Compile Revit 2022, 2025, and 2027 plus affected AutoCAD composition using
  the build skill's compile-only flags.
- [ ] Execute discovery with no Autodesk process running and prove no host
  starts.
- [ ] Run the existing Revit NUnit MTP sample and record host PID, Runner PID,
  framework ID, generation ID, and result.
- [ ] After parity, mark the remaining neutralization work in the 2026-08-12
  plan superseded and update the 2026-08-15 debug plan's Runner name.
- [ ] Stop for user review; do not commit or start P2 without authorization.

## Risks And Recovery

- **Large rename hides behavior changes.** Complete contracts/transport first,
  then rename atomically with path tests. Recovery is a group revert of Task 3.
- **Neutral DTOs become a lowest-common-denominator framework model.** Keep
  opaque IDs/payloads and provider-owned semantics; add fields only for
  cross-host control/results.
- **Type identity breaks across ALC.** Keep one loose
  `DevTools.Testing.Abstractions.dll` and add post-build ownership audits.
- **Legacy protocol diverges.** Make it an adapter to the generic request, not a
  second execution implementation.
- **NUnit parity regresses.** Keep old NUnit-specific control code until provider
  parity tests pass, then remove it in the same task group.
- **Direct rename breaks installed paths.** All consumers are in development
  scope; fail packaging if either old path remains or both executables appear.

## Progress

- [x] P0 dependency gate complete.
- [x] Neutral contracts and boundary tests complete.
- [x] Generic Transport complete.
- [x] Direct TestRunner rename complete.
- [x] Generic Host complete.
- [x] NUnit provider adapter complete; MTP/VSTest cutover still open.
- [x] Shared MTP helpers extracted; NUnit MTP run uses generic transport + `nunit` framework id.
- [x] VSTest-only compatibility path passes.
- [ ] Packaging and live host matrix pass.

## Task-local evidence

- 2026-08-17 Task 1: Abstractions built net48/net8/net10. Abstractions tests passed 10.
- 2026-08-17 Task 2: Transport built all three TFMs. Transport tests passed 12 (golden JSON, cancellation states, protocol 1 vs 2, no `testing/discover`, fake-runner capture). `NUnitProtocolGoldenTests` still passed 11. Legacy `nunit/*` serializer stays in `DevTools.NUnit.Transport` so Testing.Transport does not reference NUnit.
- 2026-08-17 Task 3: `DevTools.NUnit.Runner` renamed to `DevTools.TestRunner`. Build Debug passed. TestRunner tests passed 53. `rg DevTools.NUnit.Runner` is clean in source/tests/build/samples/docs/product/docs/agents. `--framework` defaults to `nunit`; `--debug` / `--debug-parent-pid` unchanged. Legacy NUnit CLI still omits `--framework`.
- 2026-08-17 Task 4: Testing.Host built net48/net8/net10. Host tests passed 15 (registry, cancellation, testing/* + nunit/* adapter, no discovery, generation snapshot). NUnit.Host still owns live nunit/* dispatch until Task 5 provider cutover.
- 2026-08-17 Task 5: `NUnitHostTestFrameworkProvider` wraps `INUnitHost`. TestIds become `<test>` XML (never `<name>`/display name); `ProviderPayload` is raw NUnit filter XML. Mapper keeps protocol `Id` as `TestId`. DI registers the provider + `TestingProviderRegistry`; `testing/*` is not on the pipe yet (needs host-thread marshal in Task 8, and `includeLegacyNunitEnvelopes: false` so `nunit/*` stays unique). NUnit staging kept. Proof: NUnit.Host Debug build all TFMs; new Host tests 14 (mapper/filter/provider/focused fixture); Testing.Host 16; Execution registration 1; net48 Host 13. Runtime 34 tests passed then MTP leftover-thread exit 1 (pre-existing). Host.Tests full suite still has pre-existing `Run_reports_pass_and_fail_results` trace-marker miss. Core STJ boundary fail is pre-existing.
- 2026-08-17 Task 6: `DevTools.Testing.Mtp` helpers only (error node, result properties, `TestingRunnerSession`). No universal `ITestFramework`. NUnit MTP keeps local PE discovery, FullName UIDs, builder hook. Run injects `ITestRunnerTransport` with `TestingFrameworkIds.NUnit`. Live TestRunner still emits NUnit JSON, so production MTP uses `NUnitProcessTransportAdapter` over `ProcessRunnerClient` until Task 8. Name filters round-trip via `ProviderPayload` XML. Proof: Testing.Mtp Debug all TFMs; Testing.Mtp tests 5; NUnit.Mtp tests 25; Transport tests 16 (`--framework` + `--test`).
- 2026-08-17 Task 7: VSTest executor/settings use `ITestRunnerTransport` + `TestingHostOptions` + `TestingFrameworkIds.NUnit`. Shared `NUnitProcessTransportAdapter` / `NUnitTestingMapping` live in Core `Client/` and are linked into MTP and the adapter. Discovery stays local PE. Proof: TestAdapter tests 8; NUnit.Mtp tests 25; package policy 3; net48 VSTest `--list-tests` listed `Arithmetic_runs_inside_host`, `Intentional_failure_for_demo`, `Writes_output`.

## Decisions

- 2026-08-17: Add `DevTools.Testing.Transport` because Abstractions cannot own
  JSON/IPC and TestRunner cannot depend on MTP.
- 2026-08-17: Keep provider-specific local discovery; the generic host protocol
  has no discovery endpoint.
- 2026-08-17: Rename Runner directly with no executable alias.
- 2026-08-17: Retain the legacy `nunit/*` envelope only as an adapter during
  migration.
- 2026-08-17: csproj `HostTimeout` / `HostLaunchTimeout` are forwarded to
  TestRunner as pipe/launch budgets. Adapter `WaitForExit` adds local I/O slack
  (`TestingHostTiming`) and must not mutate those CLI values.
- 2026-08-17: Keep `NUnitProcessTransportAdapter` until TestRunner serializes
  `testing/*` JSON. Do not deserialize live NUnit stdout as `TestingRunResponse`.
- 2026-08-17: Enable Polyfill for `netstandard2.0` as well as net4x so the
  VSTest adapter can compile records. ProcessRunnerClient uses the quoted
  `Arguments` path on netstandard.

## Validation

- Focused proof: Abstractions boundary/identity tests, Transport golden tests,
  provider registry tests, Runner rename tests, NUnit parity tests, no-host
  discovery tests.
- Integration proof: NUnit MTP and retained NUnit VSTest paths both call the
  single generic Runner; Revit/AutoCAD compose the provider once.
- Live proof: no host on discovery; actual NUnit test executes in the selected
  Autodesk PID with expected generation and output.
- Repository-required checks: build every touched project, Revit
  2022/2025/2027 compile-only matrix, packaging layout audit, `git diff --check`.

## Result

Complete when all NUnit behavior runs through framework-neutral control
infrastructure with one `DevTools.TestRunner.exe`, while discovery remains
host-free and both MTP and retained VSTest NUnit surfaces pass. Record remaining
IDE/debug limitations before moving the plan to `docs/plans/completed/`.
