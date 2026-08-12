# Execution Plan: NUnit Host Testing Standard Integration

Date: 2026-08-10

## Status

Superseded on 2026-08-12 by
[`2026-08-12-nunit-native-runtime-mtp.md`](2026-08-12-nunit-native-runtime-mtp.md).
Preserve completed scope evidence below; do not continue the VSTest-first,
reflection-runner, or generic debugger work from this plan.

## Outcome

Deliver one `DevTools.NUnit.Client` NuGet package and an installed
`DevTools.NUnit.Runner` CLI that run standard NUnit tests inside RevitDevTool or
AcadDevTool. Visual Studio Test Explorer, C# Dev Kit, and VSTest-mode
`dotnet test` discover and execute those tests through the standard VSTest
adapter contract. Debugging waits for a debugger to attach to the actual host
process without Visual Studio-specific APIs.

## Context

- Decision: [`0015-nunit-host-testing-standard-integration.md`](../../decisions/0015-nunit-host-testing-standard-integration.md)
- Shared execution and host boundary: `docs/architecture/Execution/README.md`,
  `docs/agents/execution-system.md`, and `docs/agents/host-boundaries.md`
- Existing host wire reference: `docs/architecture/Execution/pytest-bridge.md`
- Existing transport: `source/DevTools.Ipc/BridgeMessage.cs`,
  `source/DevTools.Execution/External/DevToolsPipeServer.cs`
- Host wiring: `source/RevitDevTool/Hosting/RevitHostingExtensions.cs` and
  `source/AcadDevTool/Hosting/AcadHostingExtensions.cs`
- Build matrix: `docs/agents/build-matrix.md`

## Global Constraints

- Public DevTools NuGet surface: **one package only**, `DevTools.NUnit.Client`.
- NUnit version: `4.6.1`; adapter version already central-pinned is
  `NUnit3TestAdapter 6.2.0` for ordinary local NUnit tests.
- NUnit host integration uses the public NUnit Test Engine API, never internal
  drivers or custom lifecycle reflection.
- VSTest is the initial `dotnet test` runner; MTP has a distinct later gate.
- Reuse `BridgeMessage`, `DevToolsPipeServer`, `IBridgeRequestHandler`, and
  `IHostContextExecutor`; NUnit payloads are separate from pytest contracts.
- Shared `DevTools.NUnit.*` code contains no Autodesk API references.
- Target matrix for shared host code: `net48;net8.0-windows;net10.0-windows`.
- Runner is `net10.0-windows`, published and bundled using the Daemon pattern.
- No `EnvDTE`, `Microsoft.VisualStudio.Interop`, Rider SDK, or IDE debugger API
  outside the VSTest object model project.
- Complete and verify one scope before starting the next. Each scope has an
  explicit rollback: revert only that scope's commit(s).

## Project and File Map

| Project / location | Responsibility |
|---|---|
| `source/DevTools.NUnit.Core/` | Host-neutral protocol DTOs, compatibility checks, test/result mapping, Runner client abstractions. |
| `source/DevTools.NUnit.Host/` | In-host NUnit Engine API adapter, assembly loader, VSTest-independent request handler, and host-process debug wait. |
| `source/DevTools.NUnit.Runner/` | `net10.0-windows` CLI: host selection/discovery, launch/reuse policy, pipe client, output and exit code. |
| `source/DevTools.NUnit.VSTestAdapter/` | Standard VSTest discoverer/executor proxy; packed but not independently published. |
| `source/DevTools.NUnit.Client/` | The only NuGet package: client API, `build/` targets, and packed adapter assets. |
| `tests/DevTools.NUnit.Core.Tests/` | Pure contract, compatibility, filter and result-mapping tests. |
| `tests/DevTools.NUnit.Host.Tests/` | Engine/package settings and handler tests using fake host executor and pipe listener. |
| `tests/DevTools.NUnit.Runner.Tests/` | CLI parse, runner discovery, exit code and output tests using a fake bridge server. |
| `tests/DevTools.NUnit.VSTestAdapter.Tests/` | Discoverer/executor mapping tests using a fake Runner client. |
| `tests/DevTools.NUnit.Integration.Tests/` | Opt-in installed-host tests; skipped unless the named Revit/AutoCAD host is available. |

## Scope 0 — Solution and Package Skeleton

**Goal:** Add empty, correctly-targeted projects and package wiring without
changing host behavior.

**Files:**

- Create the five source projects and four test projects in the Project and File
  Map.
- Modify `RevitDevTool.slnx`, `Directory.Packages.props`, and the appropriate
  `build/Modules/*` files to include compile, test, publish, and bundle paths.

**Implementation steps:**

- [ ] Add central versions for `NUnit.Engine`, `Microsoft.TestPlatform.ObjectModel`,
  and the MTP package only when its scope begins; keep version declarations in
  `Directory.Packages.props`.
- [ ] Target Core and Host at `net48;net8.0-windows;net10.0-windows`; target
  Runner at `net10.0-windows`; target the VSTest adapter at `netstandard2.0`;
  make Client package assets compatible with test projects targeting net48 and
  modern .NET.
- [ ] Add `DevTools.NUnit.Client` packing metadata but do not publish it or add
  a package feed in this scope.
- [ ] Add every project to `RevitDevTool.slnx`; do not add references from
  RevitDevTool or AcadDevTool yet.

**Tests and acceptance gate:**

- [ ] `dotnet build RevitDevTool.slnx -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false` succeeds.
- [ ] `dotnet pack source/DevTools.NUnit.Client/DevTools.NUnit.Client.csproj --no-build` produces exactly one `DevTools.NUnit.Client.*.nupkg` and no standalone DevTools.NUnit package.
- [ ] Inspect the nupkg: it contains package metadata and no RevitAPI/AcMgd DLL.

**Commit:** `chore(nunit): add standard testing project skeleton`

## Scope 1 — Core Protocol and Compatibility Contract

**Goal:** Define a small, versioned `nunit/*` contract over existing
`BridgeMessage` without an NUnit Engine dependency.

**Files:**

- Create `source/DevTools.NUnit.Core/Contracts/NUnitProtocol.cs`.
- Create `source/DevTools.NUnit.Core/Contracts/NUnitMessages.cs`.
- Create `source/DevTools.NUnit.Core/Compatibility/ProtocolCompatibility.cs`.
- Create `source/DevTools.NUnit.Core/Results/NUnitResultMapper.cs`.
- Create `tests/DevTools.NUnit.Core.Tests/ProtocolCompatibilityTests.cs` and
  `NUnitResultMapperTests.cs`.

**Interfaces produced:**

```csharp
public static class NUnitProtocol
{
    public const int CurrentVersion = 1;
    public const string Hello = "nunit/hello";
    public const string Discover = "nunit/discover";
    public const string Run = "nunit/run";
    public const string Cancel = "nunit/cancel";
    public const string Progress = "nunit/progress";
}

public sealed record NUnitHelloRequest(int ProtocolVersion);
public sealed record NUnitHelloResponse(int ProtocolVersion, string Host, string HostVersion, int ProcessId, bool IsBusy);
public sealed record NUnitDiscoverRequest(string AssemblyPath, string? Filter);
public sealed record NUnitRunRequest(Guid RunId, string AssemblyPath, string? Filter, bool WaitForDebugger);
public sealed record NUnitCaseResult(string Id, string Name, string Outcome, double DurationMilliseconds, string? Message, string? StackTrace);
```

**Implementation steps:**

- [ ] Write failing tests proving matching major protocol versions are accepted,
  mismatched versions return a deterministic compatibility error, and filters
  and failure details survive JSON round trips.
- [ ] Implement records with explicit JSON property names and one compatibility
  function; use `BridgeMessage` as the envelope rather than creating a second
  pipe/framing implementation.
- [ ] Define a normalized outcome set (`Passed`, `Failed`, `Skipped`,
  `Inconclusive`, `Error`, `Cancelled`) and map NUnit XML/event information only
  through plain string/DTO values.
- [ ] Do not add a Revit/AutoCAD-specific request property.

**Tests and acceptance gate:**

- [ ] Run `scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Core.Tests/DevTools.NUnit.Core.Tests.csproj` successfully.
- [ ] Run the Core tests once for `net48`, `net8.0-windows`, and
  `net10.0-windows`, or record the exact unavailable host/runtime blocker.
- [ ] Add golden JSON tests for `nunit/hello`, `nunit/discover`, `nunit/run`,
  and `nunit/progress`.

**Commit:** `feat(nunit): add host-test protocol contracts`

## Scope 1.5 — Assembly Load Spike

**Goal:** Validate NUnit.Engine can load a minimal test assembly in Revit
add-in load context before full NUnitHost implementation.

**Files:**

- Create spike fixture in `tests/DevTools.NUnit.Integration.Tests/` or a
  dedicated spike project.

**Implementation steps:**

- [ ] Add a minimal NUnit 4 test assembly with one passing and one failing
  fixture; no host API references in the spike assembly itself.
- [ ] Load and run the spike assembly through NUnit.Engine in-process inside a
  live Revit 2025 add-in load context; record loader settings and resolver
  behavior.
- [ ] Document any load-context or dependency-resolution blocker with exact
  exception, assembly path, and attempted settings before proceeding to Scope 2.

**Tests and acceptance gate:**

- [ ] Live Revit 2025 proof: one pass and one fail fixture execute through
  NUnit.Engine in-process, **or** a documented blocker with reproduction steps
  and proposed mitigation in `NUnitAssemblyLoader`.

**Rollback:** Revert spike only.

**Commit:** `spike(nunit): validate engine load in host context`

## Scope 2 — In-Host NUnit Engine Adapter

**Goal:** Discover and run NUnit 4 tests inside an existing host process on its
host-owned API thread.

**Files:**

- Create `source/DevTools.NUnit.Host/NUnitHost.cs`.
- Create `source/DevTools.NUnit.Host/NUnitRequestHandler.cs`.
- Create `source/DevTools.NUnit.Host/NUnitEventListener.cs`.
- Create `source/DevTools.NUnit.Host/NUnitTestPackageFactory.cs`.
- Create `source/DevTools.NUnit.Host/AssemblyLoading/NUnitAssemblyLoader.cs`.
- Modify `source/DevTools.Execution/ExecutionExtensions.cs` to register the
  handler through `IBridgeRequestHandler`.
- Modify Revit and AutoCAD hosting extensions only to opt in to the shared
  NUnit registration.

**Interfaces produced:**

```csharp
public interface INUnitHost
{
    NUnitDiscoverResponse Discover(NUnitDiscoverRequest request);
    NUnitRunResponse Run(NUnitRunRequest request, Action<NUnitProgressEvent> publish);
    void Cancel(Guid runId);
}
```

**Implementation steps:**

- [ ] Write failing fake-executor tests proving `Discover` and `Run` each call
  `IHostContextExecutor.ExecuteAsync`, and prove a `nunit/run` request does not
  invoke the test body on the pipe read loop.
- [ ] Implement `NUnitHost` through the public NUnit Engine API. Package
  settings must force the current process and no isolation domain; tests assert
  those settings before a live host is involved.
- [ ] Implement `ITestEventListener` to emit normalized `nunit/progress`
  events and a final `NUnitRunResponse`; preserve NUnit failure message and
  stack trace.
- [ ] Implement cancellation with `ITestRunner.StopRun` followed by a final
  cancelled response. The host never kills its own process.
- [ ] Add a deterministic `AssemblyLoad` failure result including the requested
  assembly path and loader exception; do not use `Assembly.LoadFrom` directly
  outside `NUnitAssemblyLoader`.
- [ ] Wire the handler into `DevToolsPipeServer`; do not modify pytest request
  models, methods, or runner scripts.
- [ ] Generalize `DevToolsPipeServer` `NotifySender` beyond `PytestRequestHandler`
  (interface or convention for progress publishers) so NUnit `nunit/progress`
  events use the same notification path without pytest-specific coupling.
- [ ] Document `ExecutionGuard`/dialog suppression policy for NUnit: match
  pytest `Suppress` mode on Revit; document the AutoCAD gap where equivalent
  suppression is not yet available.

**Tests and acceptance gate:**

- [ ] `DevTools.NUnit.Host.Tests` proves discovery, pass, assertion failure,
  test output, cancellation, incompatible protocol, and load failure.
- [ ] `DevTools.Execution.Tests` proves `AddExecutionServices()` registers both
  pytest and NUnit handlers without duplicate method names.
- [ ] Compile-only builds pass for Autodesk 2022, 2025, and 2027 with deploy
  disabled, using the build skill commands.
- [ ] Live Revit 2025 proof: a fixture creates no host API objects and returns
  one pass plus one assertion failure through the named pipe.

**Commit:** `feat(nunit): execute NUnit tests in host context`

## Scope 3 — Runner CLI and Installed Distribution

**Goal:** Ship one net10 Windows controller that locates a compatible host,
speaks the Core contract, and returns CI-correct output and exit codes.

**Files:**

- Create `source/DevTools.NUnit.Runner/Program.cs`.
- Create `source/DevTools.NUnit.Runner/Commands/DiscoverCommand.cs` and
  `RunCommand.cs`.
- Create `source/DevTools.NUnit.Runner/Services/HostLocator.cs` and
  `NUnitPipeClient.cs`.
- Modify `build/Modules/PublishDaemonModule.cs` or add a dedicated publish
  module, then modify `CreateBundleModule.cs` to copy Runner beside the Daemon.

**Command contract:**

```text
DevTools.NUnit.Runner discover <assembly> --host <Revit|AutoCAD> --version <year>
DevTools.NUnit.Runner run <assembly> --host <Revit|AutoCAD> --version <year> [--filter <nunit-where>] [--debug wait]
```

**Implementation steps:**

- [ ] Write parser tests for required assembly, host, version, filter, and
  `--debug wait`; reject unknown options with exit code 2.
- [ ] Implement discovery using the existing `HostPipeName`/host discovery
  conventions, then validate `nunit/hello` before any discover/run request.
- [ ] Implement result streaming to stdout, error diagnostics to stderr, and
  exit codes: `0` all passed, `1` test failure/error/cancel, `2` CLI or
  compatibility error, `3` no compatible host.
- [ ] Implement launch/reuse only through the repository's existing host launch
  service or a small extracted host-neutral interface; do not copy ricaun's
  Revit process or Visual Studio launch helpers.
- [ ] Publish a self-contained `win-x64` Runner and bundle it using the same
  installation location policy as `DevTools.Daemon`.

**Tests and acceptance gate:**

- [ ] `DevTools.NUnit.Runner.Tests` uses a fake pipe server to prove hello,
  discovery, progress rendering, exit codes, timeout, and incompatible-host
  behavior.
- [ ] `dotnet publish source/DevTools.NUnit.Runner -c Release -r win-x64`
  succeeds and the bundle contains the Runner exactly once.
- [ ] Manual installed-host proof: `Runner discover` and `Runner run` against
  Revit 2025 return the expected test list and failure exit code.

**Commit:** `feat(nunit): add installed host-test runner`

## Scope 4 — VSTest Proxy Adapter and Client NuGet Package

**Goal:** Make standard Test Explorer and VSTest-mode `dotnet test` invoke the
Runner, while publishing only `DevTools.NUnit.Client`.

**Files:**

- Create `source/DevTools.NUnit.VSTestAdapter/DevToolsNUnitDiscoverer.cs`.
- Create `source/DevTools.NUnit.VSTestAdapter/DevToolsNUnitExecutor.cs`.
- Create `source/DevTools.NUnit.VSTestAdapter/RunnerClientFactory.cs`.
- Create `source/DevTools.NUnit.Client/build/DevTools.NUnit.Client.targets`.
- Modify `source/DevTools.NUnit.Client/DevTools.NUnit.Client.csproj` to pack
  adapter build assets into its nupkg.
- Create a sample `samples/DevTools.NUnit.SampleTests/` with a normal NUnit
  fixture and no Revit API code in the adapter itself.

**Interfaces produced:**

```csharp
[FileExtension(".dll")]
public sealed class DevToolsNUnitDiscoverer : ITestDiscoverer;

public sealed class DevToolsNUnitExecutor : ITestExecutor;

public interface IRunnerClient
{
    IReadOnlyList<RemoteTestCase> Discover(string source);
    RemoteRunResult Run(IReadOnlyList<RemoteTestCase> tests, bool waitForDebugger);
}
```

**Implementation steps:**

- [ ] Write discoverer tests where a fake `IRunnerClient` returns parameterized
  NUnit cases; assert their source, fully-qualified name, display name, traits,
  and stable IDs are converted to `TestCase` correctly.
- [ ] Write executor tests proving VSTest selection/filter input becomes one
  Runner call and each remote started/finished event is reported through
  `IFrameworkHandle` with the mapped outcome and stack trace.
- [ ] Implement `Cancel()` to request Core cancellation and complete pending
  cases as cancelled; do not kill Revit/AutoCAD unless Runner owns a process it
  launched and its documented escalation timeout has elapsed.
- [ ] Package adapter assets with the public Client nupkg using the conventional
  VSTest adapter folder layout. Ensure the package is copied/discovered from a
  test project's build output without a VSIX.
- [ ] Add package properties `DevToolsNUnitRunnerPath`, `DevToolsNUnitHost`,
  and `DevToolsNUnitHostVersion`; resolution order is explicit MSBuild property,
  installed Runner on PATH, then a documented installation registry value.
- [ ] Keep `NUnit3TestAdapter` for ordinary local NUnit projects. Host-test
  samples opt into the DevTools adapter explicitly so two adapters cannot claim
  the same tests accidentally.

**Tests and acceptance gate:**

- [ ] Adapter unit tests prove discovery, selected-test run, filter run, pass,
  failure, skip, error, cancellation, output, and Runner-not-found diagnostics.
- [ ] Pack then restore the sample from a local nupkg source; `dotnet test`
  under VSTest discovers and invokes the DevTools adapter.
- [ ] Visual Studio Test Explorer manual proof: test tree, a pass, an assertion
  failure, output, and rerun all work through a live Revit 2025 host.
- [ ] C# Dev Kit manual proof: the same net8 sample discovers and runs. Record
  that net48 host-targeted projects require attach debugging, not C# Dev Kit
  debugging.
- [ ] Rider compatibility proof: run the same sample using its VSTest proxy
  configuration; capture version and logs. A failure blocks Rider support but
  does not invalidate the VSTest release gate.

**Commit:** `feat(nunit): proxy host tests through VSTest`

## Scope 5 — Host-Process Debugging

**Goal:** Debug the actual host process without VS/Rider-specific runtime APIs.

**Files:**

- Create `source/DevTools.NUnit.Host/Debugging/DebuggerWaiter.cs`.
- Create `source/DevTools.NUnit.Core/Contracts/NUnitDebugMessages.cs`.
- Modify Runner commands and the VSTest executor to pass `WaitForDebugger`.
- Create `tests/DevTools.NUnit.Host.Tests/DebuggerWaiterTests.cs`.

**Implementation steps:**

- [ ] Write tests using an injected `IDebuggerState` seam; no test depends on
  an installed IDE. The default implementation reads
  `System.Diagnostics.Debugger.IsAttached`.
- [ ] Host publishes `nunit/debug-ready` containing host PID and waits before
  scheduling execution; it does not block the pipe server receive loop.
- [ ] Runner prints an attach instruction containing the host executable and
  PID; timeout returns a cancelled/diagnostic result without terminating a
  reused host.
- [ ] Map the VSTest standard debug intent to `WaitForDebugger`; do not add
  EnvDTE, `Microsoft.VisualStudio.Interop`, or JetBrains dependencies.
- [ ] Treat `Debugger.Break()` as a later opt-in only after attach behavior is
  proven; it is not part of this scope's required behavior.

**Tests and acceptance gate:**

- [ ] Unit tests prove immediate attach, delayed attach, timeout, cancellation,
  and no-IDE paths.
- [ ] Live Revit proof: start `Runner run --debug wait`, attach Visual Studio or
  Rider manually to the displayed Revit PID, hit a breakpoint in the test
  assembly, and receive the final test result.

**Commit:** `feat(nunit): support host-process debugger attach`

## Scope 6 — Microsoft Testing Platform Extension

**Goal:** Add MTP-mode `dotnet test` support through official MTP extension
points, without changing the VSTest behavior or publishing a second package.

**Files:**

- Create `source/DevTools.NUnit.Mtp/` with official MTP discovery and execution
  extension implementations.
- Create `tests/DevTools.NUnit.Mtp.Tests/` with protocol-to-MTP mapping tests.
- Modify Client package packing to include MTP assets only after its standalone
  sample passes.
- Add `samples/DevTools.NUnit.Mtp.SampleTests/` and a scoped `global.json`
  selecting `Microsoft.Testing.Platform`.

**Implementation steps:**

- [ ] First write an executable compatibility test proving a VSTest-only Client
  package emits a clear error under MTP instead of running the test locally.
- [ ] Implement discovery and execution with the official MTP extension API;
  reuse only `IRunnerClient` and Core DTOs from Scope 4.
- [ ] Ensure MTP filter, cancellation, result, output, and exit-code behavior
  match the VSTest adapter's observable behavior.
- [ ] Pack MTP assets into `DevTools.NUnit.Client`; do not add a second public
  PackageId.

**Tests and acceptance gate:**

- [ ] The MTP sample passes `dotnet test` with the scoped MTP `global.json`.
- [ ] The original VSTest sample still passes under the default VSTest runner.
- [ ] CI executes both samples against the fake Runner and records their
  discovered case names and outcomes as equal.

**Commit:** `feat(nunit): support Microsoft Testing Platform`

## Scope 7 — Documentation, Release, and Multi-Host Verification

**Goal:** Publish an operable package and preserve product/architecture truth.

**Files:**

- Modify `docs/architecture/Execution/README.md` with a link to one focused
  NUnit-host testing page.
- Create `docs/architecture/Execution/nunit-host-testing.md`.
- Modify `docs/product/execution.md` with the observable CLI/test-host behavior.
- Modify `docs/agents/mcp-pytest-bridge.md` only to state the NUnit pipe uses
  the same envelope but is a separate contract.
- Modify packaging modules and release notes as required by actual artifacts.

**Implementation steps:**

- [ ] Document install, the single PackageReference, host selection, VSTest vs
  MTP selection, and debugger attach by PID.
- [ ] Document the exact supported Revit/AutoCAD versions based on verified
  matrix runs; do not infer support from compile-only success.
- [ ] Add an opt-in integration matrix that runs pass/fail/filter/cancel tests
  against each available Revit and AutoCAD host.
- [ ] Verify one installed package from a clean sample solution and restore it
  from the release feed before publishing.

**Tests and acceptance gate:**

- [ ] Unit suite is green.
- [ ] Autodesk 2022, 2025, and 2027 compile gates are green.
- [ ] Revit and AutoCAD live-host evidence is recorded separately; a missing
  installed host is reported as a blocker rather than a pass.
- [ ] Local NuGet restore + `dotnet test` VSTest proof is green; MTP proof is
  green only after Scope 6.

**Commit:** `docs(nunit): document host test integration`

## Risks And Recovery

- **NUnit engine cannot load a test assembly in a host load context.** Contain
  resolution in `NUnitAssemblyLoader`, reproduce with one fixture, and block
  Scope 2. Revert only the Host scope if no supported resolver is possible.
- **VSTest adapter discovery launches a host or is too slow.** First measure
  local engine discovery; only add host-backed discovery if its semantics are
  required. Do not silently launch Revit while an IDE refreshes tests.
- **Two adapters discover the same host test.** Require explicit host-test
  opt-in and assert one owner in the sample package test.
- **Rider does not invoke the VSTest adapter consistently.** Capture logs in
  Scope 4 and mark Rider unsupported; do not add a Rider SDK dependency as an
  unreviewed workaround.
- **MTP contract differs from VSTest.** Keep Scope 6 isolated and package MTP
  assets only after both samples prove equivalent observable outcomes.
- **Runner/add-in protocol versions drift.** `nunit/hello` rejects incompatible
  major versions before discovery or execution; rollback the Runner or add-in
  release as a matched installer artifact.

## Progress

- [x] Decision 0015 accepted.
- [x] Scope sequence and acceptance gates written.
- [x] Scope 0 complete.
- [x] Scope 1 complete.
- [x] Scope 1.5 complete (unit spike; live Revit load-context proof deferred to Scope 2 gate).
- [x] Scope 2 complete (unit + compile; live Revit pipe proof pending manual host).
- [ ] Scope 3 complete.
- [ ] Scope 4 complete.
- [ ] Scope 5 complete.
- [ ] Scope 6 complete.
- [ ] Scope 7 complete.

## Decisions

- 2026-08-10: Publish one public package (`DevTools.NUnit.Client`) while
  retaining separate implementation projects for host runtime, Runner, VSTest,
  and MTP responsibilities.
- 2026-08-10: VSTest precedes MTP because it is the default `dotnet test`
  runner and the common integration route for Visual Studio and C# Dev Kit.
- 2026-08-10: Debugging attaches to the host PID using standard .NET debugger
  state; no Visual Studio-specific automation is permitted.
- 2026-08-10: VSTest discovery strategy — default hybrid: local NUnit engine for
  metadata when the assembly has no host API references; host-backed
  `nunit/discover` when traits indicate `HostTest` or the assembly references
  host facades.
- 2026-08-11: `DevTools.NUnit.Client` exposes only
  `DevTools.NUnit.VSTestAdapter.dll` under `lib/netstandard2.0`. This is the
  standard package signal that causes Rider and other VSTest-capable IDEs to
  select the VSTest provider. Its private runtime sidecars are kept under
  `tools/netstandard2.0` and copied after build; they must not pollute the
  consuming test project's compile graph.
- 2026-08-11: Rejected an ILRepack single-DLL package experiment. Although the
  adapter could be packed below `lib/netstandard2.0`, VSTest failed to reflect
  over it because merged `Microsoft.Bcl.AsyncInterfaces` identities could not
  be resolved. Keep TestPlatform-adjacent adapter dependencies as separate
  files; this experiment does not establish a Rider execution fix.
- 2026-08-11: The source sample now mirrors ricaun's IDE-facing shape: a static
  `ProjectReference` to `DevTools.NUnit.VSTestAdapter`, no dynamic source-tree
  adapter target import, and host configuration from its `.runsettings`. CLI
  VSTest proof records the DevTools executor URI; Rider remains the pending
  live IDE gate.
- 2026-08-11: Rider live proof rejected the static-reference hypothesis. The
  10:22 Rider session still selected `NUnit 3x-4x` with
  `NUnitTestRunnerRunStrategy`, then executed the test in the ReSharper test
  process and failed to resolve `RevitAPI`. The adapter reference is present,
  but Rider's native NUnit provider owns the test node; do not treat adapter
  packaging changes as a fix for this provider-selection problem.
- 2026-08-11: Collapse the public surface to one package,
  `DevTools.NUnit.TestAdapter`. Remove `DevTools.NUnit.Client` and all MSBuild
  target workarounds; the adapter project is packable directly and the sample
  references it with a normal `ProjectReference`.

## Validation

- Focused proof: each scope lists its unit and packaging gate.
- 2026-08-11: `DevTools.NUnit.VSTestAdapter.Tests` passes; a locally packed
  Client package restored into a clean net8.0-windows consumer, copied
  `DevTools.NUnit.VSTestAdapter.dll` plus its private runtime sidecars from the
  NuGet cache, built without compile dependency conflicts, and was discovered
  by `dotnet vstest`. Rider explorer/execution remains a live IDE gate.
- Integration proof: Scope 2 onward requires a fake pipe server; live host
  proof begins with one Revit fixture and expands to Revit/AutoCAD in Scope 7.
- Repository-required checks: use the build skill after every `.cs`/`.csproj`
  edit; compile host-sensitive scopes for Autodesk 2022, 2025, and 2027 with
  deployment disabled.

## Result

Implementation through Scope 2 (2026-08-10).

### Verified commands

```powershell
dotnet build RevitDevTool.slnx -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Core.Tests/DevTools.NUnit.Core.Tests.csproj   # 30 passed
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj   # 12 passed
scripts/test-dotnet.ps1 -Project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -Filter BridgeHandlerRegistration  # 1 passed
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
dotnet build source/DevTools.NUnit.Client/DevTools.NUnit.Client.csproj -c Release -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
dotnet pack source/DevTools.NUnit.Client/DevTools.NUnit.Client.csproj --no-build -c Release
```

### Artifacts

- 9 projects + spike fixtures in `RevitDevTool.slnx`
- `DevTools.NUnit.Client.1.0.0.nupkg` (placeholder lib; adapter assets in Scope 4)
- Protocol v1: `nunit/hello`, `discover`, `run`, `cancel`, `progress`

### Limitations / open gates

- Live Revit 2025 pipe proof (`nunit/discover` + `nunit/run`) not recorded yet
- `build/Modules/*` publish/bundle paths for Runner deferred to Scope 3
- Core.Tests run on `net10.0-windows` only (library builds net48/net8/net10)
- Client nupkg uses `SuppressDependenciesWhenPacking` until Scope 4 embeds assemblies
