# NUnit Native Runtime And MTP-First Integration Plan

**Goal:** Run full NUnit 4.6.1 semantics inside Autodesk hosts through a native
framework runtime, with MTP as the sole IDE/Test Explorer compatibility layer.

**Architecture:** Build one immutable framework generation per test-output
content set. Load it through a collectible ALC on modern .NET or a
generation-aware no-context loader on net48, and expose only neutral Core DTOs
over the existing host pipe.

**Tech Stack:** C#/.NET Framework 4.8, .NET 8, .NET 10, NUnit 4.6.1,
Microsoft.Testing.Platform 2.3.2, Named Pipes, xUnit for DevTools unit tests,
Revit/AutoCAD live-host verification.

Date: 2026-08-12

## Status

Accepted — **P0 isolation gate closed** (live `generation.unloaded` waived in
ADR 0016 §4). Remaining framework-neutral extraction lives in
[2026-08-17 P1](2026-08-17-p1-framework-neutral-testing-core.md). VSTest
removal is [0022](../../decisions/0022-nunit-mtp-only-testing-stack.md). Do
not execute remaining Rider / C# Dev Kit attach or VSTest-adapter gates, and
do not run `scripts/test-dotnet.ps1` (removed).

## Outcome

Replace the in-host NUnit reflection emulator with the real NUnit 4.6.1
framework runtime, preserve hot reload without locking test build output, and
provide one host-neutral execution protocol consumed by Microsoft Testing
Platform 2.3.2 through the installed Runner. MTP owns discovery/result
compatibility with IDEs; the DevTools MTP extension owns no process lifecycle.
Debugger automation is a final optional IDE-specific phase and does not block
the native NUnit/MTP release.

## Decision And Existing Context

- Decision:
  [`0016-nunit-native-runtime-and-mtp-first-integration.md`](../../decisions/0016-nunit-native-runtime-and-mtp-first-integration.md)
- Historical decision:
  [`0015-nunit-host-testing-standard-integration.md`](../../decisions/0015-nunit-host-testing-standard-integration.md)
- Current product behavior:
  [`host-testing.md`](../../product/host-testing.md)
- Native host path: `NUnitRuntimeManager` + TFM session factory (`NUnitReflectionRunner` deleted).
- Public consumer package (P1): `DevTools.NUnit` from `source/DevTools.NUnit.Mtp/`.
- Current probe loader:
  `source/DevTools.Utilities/AssemblyLoading/DirectoryAssemblyLoader.cs`
- Current protocol: `source/DevTools.NUnit.Core/Contracts/NUnitMessages.cs`
- Existing IDE evidence: Rider selected its native NUnit provider and executed
  outside Revit even though the DevTools VSTest adapter was present.
- Build matrix: Revit 2022-2024 `net48`, Revit 2025-2026
  `net8.0-windows`, Revit 2027 `net10.0-windows`.

## Global Constraints

- NUnit framework version for the first supported runtime is exactly `4.6.1`.
- MTP package version for the first implementation spike is exactly `2.3.2`.
- NUnit performs discovery, lifecycle, data expansion, assertions, filtering,
  and result semantics. DevTools does not reproduce them.
- `NUnit.Engine` and NUnit agent processes do not run inside or outside the
  Autodesk host for test execution.
- Shared `DevTools.NUnit.*` projects contain no Autodesk API types.
- Known host API names and shared prefixes reuse matching assemblies already
  loaded by the host. The neutral Core contract keeps one cross-boundary type
  identity. This is a preference for known cases, not a closed-world rule;
  NUnit, the runtime bootstrap, test code, and resolved private dependencies
  belong to the generation.
- One framework generation is immutable. A rebuild creates a new generation.
- Modern generations use one collectible ALC. net48 generations are not
  unloadable and remain until host exit.
- Host test execution is serialized. NUnit worker count is `1` until a later
  decision proves safe parallel host execution.
- MTP maps IDE requests to Runner/Core DTOs; no VSTest adapter is shipped.
- MTP contains no process activation or ownership and no Autodesk host locator,
  launcher, reuse, termination, or debugger-attach implementation. It only
  maps MTP messages to an injected Runner transport. Runner and its launcher
  infrastructure own those policies.
- Runtime NUnit discovery is authoritative. Source generation cannot create or
  expand NUnit cases.
- C# Dev Kit does not claim net48 debugger support. Its required debugger gate
  is Revit 2025+; net48 uses Visual Studio or Rider/manual attach.
- No debugger abstraction or attach handshake is added to Core, Host, Runtime,
  Runner protocol, or MTP.
- `EnvDTE`, Visual Studio interop, Rider SDK, and VS Code extension dependencies
  are allowed only in an optional final IDE-specific integration project.
- Do not stage or commit plan or implementation changes unless the user asks.

## Agent Operational Workflow

These are execution safeguards for agents, not additional product semantics:

- Live proof must show that tests execute in the intended Revit process and
  exercise real Revit API behavior. The exact acceptance assembly is flexible:
  Task 1 owns framework-semantics coverage, while a host smoke sample such as
  `samples/DevTools.NUnit.SampleTests` proves Revit API execution through a real
  `dotnet test` invocation. A temporary MTP spike sample may be used before the
  existing sample is converted, but it is not a second public consumer surface.
- Agents never start `Revit.exe` directly. They invoke
  `DevTools.NUnit.Runner`, which locates, reuses, or launches the host.
- Keep live verification operationally controlled: use one Revit process when
  practical, record the selected PID, and do not spawn additional hosts without
  first accounting for the existing process. This is an agent workflow rule,
  not a production singleton constraint on Runner.
- Only one agent owns the live-host lane at a time. Other agents may compile or
  run host-free focused tests, but may not launch, terminate, deploy to, or
  republish components used by the active live session.
- After changes to Core, Host, or Runtime that flow into the add-in, run focused
  tests and compile `RevitDevTool` for the affected TFM, with 2022, 2025, and
  2027 as the net48/net8/net10 spot-check matrix.
- Before a build that deploys a changed add-in, run
  `scripts/kill-host.ps1 -HostApp Revit -Year <year>`, then
  `scripts/build-host.ps1 -Year <year>`. Compile-only builds with deploy flags
  disabled do not require host termination.
- After changes to Runner or Core contracts consumed by Runner, publish the
  installed executable with
  `dotnet publish source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Release -r win-x64`.
  Public package publication remains blocked by the P0/P1 gates; this required
  local publish is deployment proof, not a public release.

## Scope

In scope:

- Real NUnit discovery and execution through public NUnit framework APIs.
- A neutral framework-runtime contract shared across load boundaries.
- Content-addressed coherent shadow generations.
- Collectible ALC loading for net8/net10.
- Controlled no-context generation loading for net48.
- Revit 2024 plus Dynamo conflict proof and modern-host unload proof.
- MTP discovery, execution, filtering, cancellation, output, and attachments.
- Runner-owned host locate/launch/reuse policy behind the MTP boundary.
- An IDE run matrix proving provider ownership and Autodesk host execution.
- Removal of `NUnitReflectionRunner` after the native path passes P0.

Out of scope:

- NUnit Engine process agents or engine extension loading.
- Concurrent Revit API test execution.
- AppDomain remoting of `UIApplication`, `Document`, fixtures, or test cases.
- Reimplementing NUnit features through reflection or source generation.
- A VSTest compatibility adapter or MTP VSTest Bridge.
- Automatic C# Dev Kit debugging of Revit 2022-2024.
- A generic cross-IDE debugger attach protocol.
- Supporting arbitrary NUnit major versions in the first release.
- Adding xUnit, MSTest, or TUnit drivers before the NUnit runtime boundary is
  proven. The boundary must allow those later without implementing them now.

## Target Architecture

```text
Visual Studio / Rider / C# Dev Kit
                 |
        MTP ITestFramework proxy
                 |
      installed DevTools.NUnit.Runner
                 |
 host locate/launch/reuse + existing pipe
                 |
      NUnitRequestHandler on host context
                 |
          NUnitRuntimeManager
          /                 \
 net8/net10 collectible ALC  net48 coherent generation
          \                 /
       DevTools.NUnit.Runtime
                 |
 NUnitTestAssemblyRunner / ITestAssemblyRunner
                 |
       user NUnit test assembly
```

The only cross-generation assembly contract is
`DevTools.NUnit.Core`. The runtime maps all NUnit objects to Core DTOs before
returning to the host. No NUnit type appears in a Host, Runner, or MTP public
signature. MTP neither starts nor owns Runner and never launches, kills,
reuses, or attaches to Revit/AutoCAD. The installed package supplies a Runner
transport; Runner activation is handled outside the MTP framework extension.

## Project And File Map

| Project / file | Responsibility |
|---|---|
| `source/DevTools.NUnit.Core/Runtime/INUnitRuntimeSession.cs` | Neutral runtime boundary and event sink. |
| `source/DevTools.NUnit.Core/Contracts/NUnitMessages.cs` | Protocol v2 discovery, result, generation, and attachment DTOs. |
| `source/DevTools.NUnit.Runtime/` | The only production project referencing `NUnit`; maps public NUnit runner APIs to Core DTOs. |
| `source/DevTools.NUnit.Host/Loading/NUnitGenerationBuilder.cs` | Builds one immutable coherent shadow directory. |
| `source/DevTools.NUnit.Host/Loading/NUnitRuntimeLoadContext.cs` | Collectible net8/net10 framework ALC. |
| `source/DevTools.NUnit.Host/Loading/NetFrameworkNUnitGeneration.cs` | net48 resolver keyed by requesting assembly and generation. |
| `source/DevTools.NUnit.Host/NUnitRuntimeManager.cs` | Selects loader, owns sessions, serializes runs, exposes diagnostics. |
| `source/DevTools.NUnit.Runner/` | Owns host locate/launch/reuse policy and the CLI/pipe session. |
| `source/DevTools.NUnit.Mtp/` | MTP 2.3.2 compatibility extension; maps IDE requests exclusively to Runner/Core. |
| `tests/DevTools.NUnit.Runtime.Fixtures/` | Full-semantics fixture matrix and generation marker. |
| `tests/DevTools.NUnit.Runtime.Tests/` | Native runner contract and mapping tests. |
| `tests/DevTools.NUnit.Host.Tests/` | Generation, ALC, net48 resolver, and handler tests. |
| `tests/DevTools.NUnit.Mtp.Tests/` | MTP request/result/filter mapping and process-boundary tests. |
| `samples/DevTools.NUnit.Mtp.SampleTests/` | SDK-style MTP sample used by VS, Rider, and C# Dev Kit gates. |

## Runtime Interfaces

Task 2 must add the following host-neutral interfaces to Core. Later tasks
consume these exact names rather than referencing the concrete runtime:

```csharp
public interface INUnitRuntimeSession : IDisposable
{
    string GenerationId { get; }
    NUnitDiscoverResponse Discover(NUnitDiscoverRequest request);
    NUnitRunResponse Run(
        NUnitRunRequest request,
        INUnitRuntimeEventSink eventSink,
        CancellationToken cancellationToken);
    void Cancel(Guid runId);
}

public interface INUnitRuntimeEventSink
{
    void Publish(NUnitRuntimeEvent runtimeEvent);
}

public sealed record NUnitRuntimeEvent(
    Guid RunId,
    string Kind,
    NUnitCaseResult? Case,
    string? Message,
    NUnitAttachment? Attachment);
```

`DevTools.NUnit.Runtime` implements `INUnitRuntimeSession`. The loader must bind
its reference to `DevTools.NUnit.Core` to the already-loaded host copy so the
cast remains valid across ALC/no-context boundaries.

## P0 â€” Correct NUnit Runtime

### Task 0: Correct assembly ownership before extending the runtime

**Files:**

- Modify: `source/DevTools.NUnit.Core/DevTools.NUnit.Core.csproj`
- Modify: `source/DevTools.NUnit.Core/Contracts/NUnitMessages.cs`
- Move: Core JSON/IPC implementation to the Host/Runner transport boundary.
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitSharedAssemblyPolicy.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitGenerationBuilder.cs`
- Modify: `source/RevitDevTool/RevitDevTool.csproj`
- Modify: `source/ACadDevTool/ACadDevTool.csproj`
- Test: `tests/DevTools.NUnit.Core.Tests/`
- Test: `tests/DevTools.NUnit.Host.Tests/`

- [x] Add a failing contract-boundary test proving Core references neither
  `DevTools.Ipc` nor `System.Text.Json`.
- [x] Make Core contain only plain DTOs, runtime interfaces, protocol constants,
  and transport-neutral compatibility data.
- [x] Keep exactly one loose Core assembly for Host and Runtime type identity.
- [x] Add failing generation tests proving package assemblies such as
  `System.Reflection.Metadata` are generation-private while true platform and
  Autodesk API assemblies remain host-shared.
- [x] Package the complete Runtime private dependency closure rather than only
  `DevTools.NUnit.Runtime.dll`.
- [x] Replace host wildcard/restore behavior with explicit ownership: host
  implementation dependencies may merge; Core, Runtime, NUnit, and Runtime
  private dependencies must never merge into the host.
- [x] Delete the NUnit satellite snapshot/restore targets. A dependency must
  never be both embedded in the host and copied loose beside it.
- [x] Add post-build assembly-reference/layout proof for net48 and modern hosts.

Task 0 exit gate: Core is dependency-free, no assembly is both embedded and
loose, the Runtime generation contains its complete private closure, and host
build output satisfies the ownership audit before Tasks 1-8 continue.

### Task 1: Freeze the full-semantics acceptance fixture

**Files:**

- Create: `tests/DevTools.NUnit.Runtime.Fixtures/DevTools.NUnit.Runtime.Fixtures.csproj`
- Create: `tests/DevTools.NUnit.Runtime.Fixtures/AssemblySetUp.cs`
- Create: `tests/DevTools.NUnit.Runtime.Fixtures/FullSemanticsFixture.cs`
- Create: `tests/DevTools.NUnit.Runtime.Fixtures/ParameterizedFixture.cs`
- Create: `tests/DevTools.NUnit.Runtime.Fixtures/GenerationMarker.cs`
- Create: `tests/DevTools.NUnit.Runtime.Fixtures/TestData.cs`
- Modify: `RevitDevTool.slnx`

**Produces:** one NUnit 4.6.1 DLL containing stable cases for every P0 semantic.

- [x] Add fixtures covering `Test`, multiple `TestCase` attributes,
  `TestCaseSource`, `TestFixtureSource`, generic/parameterized fixtures,
  namespace `SetUpFixture`, one-time and per-test setup/teardown, async test and
  async lifecycle, `Retry`, `Repeat`, `Order`, `Explicit`, `Ignore`, category,
  custom property, multiple assertions, warning, inconclusive, output, and a
  deliberate unexpected exception.
- [x] Make each lifecycle method append a unique token to
  `%TEMP%/DevTools/NUnitAcceptance/<run-id>.log`; assertions must verify NUnit's
  ordering rather than DevTools ordering.
- [x] Expose `GenerationMarker.Value` as a compile constant initially equal to
  `generation-one`; Task 7 rebuilds the fixture with `generation-two`.
- [x] Add a case whose `TestCaseSource` executes code so metadata/source-gen
  discovery cannot accidentally satisfy the acceptance gate.
- [x] Build the fixture for `net48`, `net8.0-windows`, and
  `net10.0-windows`.

Run:

```powershell
dotnet build tests/DevTools.NUnit.Runtime.Fixtures/DevTools.NUnit.Runtime.Fixtures.csproj -c Debug
```

Expected: all three target outputs contain `nunit.framework.dll` version 4.6.1
and matching PDBs.

### Task 2: Add protocol v2 and the neutral runtime boundary

**Files:**

- Create: `source/DevTools.NUnit.Core/Runtime/INUnitRuntimeSession.cs`
- Modify: `source/DevTools.NUnit.Core/Contracts/NUnitMessages.cs`
- Delete: `source/DevTools.NUnit.Core/Contracts/NUnitDebugMessages.cs`
- Modify: `source/DevTools.NUnit.Core/Contracts/NUnitProtocol.cs`
- Modify: `source/DevTools.NUnit.Core/Contracts/NUnitJsonContext.cs`
- Modify: `tests/DevTools.NUnit.Core.Tests/NUnitProtocolGoldenTests.cs`
- Modify: `tests/DevTools.NUnit.Core.Tests/ProtocolCompatibilityTests.cs`

**Produces:** protocol version `2` and the interfaces listed in â€œRuntime
Interfacesâ€.

- [x] First add golden JSON tests for traits/properties, source file/line,
  parent test ID, skip reason, attachments, generation ID, and runtime
  diagnostic. Host-session ownership is Runner-authored Task 9 data and must
  not appear in the in-host `nunit/hello` response.
- [ ] Run the focused tests and confirm they fail because v1 lacks those fields.
- [x] Add `NUnitAttachment`, `NUnitSourceLocation`, `NUnitTrait`, and
  `NUnitRuntimeDiagnostic` records.
- [x] Extend `NUnitDiscoveredTest` and `NUnitCaseResult` without putting NUnit
  types or XML in the wire contract.
- [x] Remove `WaitForDebugger`, `NUnitDebugReadyEvent`, and the
  `nunit/debug-ready` method from protocol v2. Debugger orchestration is not a
  Core/host protocol responsibility.
- [x] Change `NUnitProtocol.CurrentVersion` to `2`; reject v1 with the existing
  compatibility error instead of silently interpreting it.
- [x] Add the neutral runtime interfaces exactly as declared above.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Core.Tests/DevTools.NUnit.Core.Tests.csproj
```

Expected: all golden and compatibility tests pass; no Core project reference to
NUnit exists.

### Task 3: Build immutable coherent generations

**Files:**

- Create: `source/DevTools.NUnit.Host/Loading/NUnitGenerationManifest.cs`
- Create: `source/DevTools.NUnit.Host/Loading/NUnitGenerationBuilder.cs`
- Create: `source/DevTools.NUnit.Host/Loading/NUnitSharedAssemblyPolicy.cs`
- Create: `tests/DevTools.NUnit.Host.Tests/NUnitGenerationBuilderTests.cs`
- Modify: `source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj`

**Produces:**

```csharp
public sealed record NUnitGenerationManifest(
    string GenerationId,
    string SourceAssemblyPath,
    string ShadowDirectory,
    string ShadowAssemblyPath,
    string RuntimeAssemblyPath,
    string FrameworkAssemblyPath,
    IReadOnlyList<string> ManagedAssemblies,
    IReadOnlyList<string> NativeAssets,
    string? SymbolPath);

public interface INUnitGenerationBuilder
{
    NUnitGenerationManifest Build(string testAssemblyPath);
}
```

- [x] Write a failing test that compiles/copies two assemblies with the same
  name and version but different `GenerationMarker.Value`; assert distinct
  generation IDs and immutable shadow directories.
- [x] Write a failing test proving a source DLL and PDB remain writable after
  generation creation and load.
- [x] Compute `GenerationId` from SHA-256 over relative path, length, and file
  content for the test DLL, PDB, managed dependencies, runtime assembly, and
  native assets. Do not use timestamp XOR length.
- [x] Copy the complete test-output tree into
  `%TEMP%/DevTools/NUnit/Generations/<generation-id>/`; preserve relative native
  runtime subdirectories.
- [x] Copy `DevTools.NUnit.Runtime.dll` and its PDB into the same generation.
- [x] Validate exactly one `nunit.framework.dll` 4.6.1 exists in the
  generation. Fail before load when absent or mismatched.
- [x] Reuse assemblies already loaded by the host for the known Autodesk host
  names and shared prefixes used by the existing host loaders. Do not enumerate
  the complete .NET runtime assembly surface and do not treat this preference as
  a strict boundary for every dependency.
- [x] Never delete a generation from this component. Cleanup is a later
  process-start maintenance operation that only removes directories not used by
  a live process.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj -Filter NUnitGenerationBuilder
```

Expected: both real IL generations are observed, source files remain writable,
and the generated manifest lists one coherent directory.

### Task 4: Implement the real NUnit runtime adapter

**Files:**

- Create: `source/DevTools.NUnit.Runtime/DevTools.NUnit.Runtime.csproj`
- Create: `source/DevTools.NUnit.Runtime/NUnitRuntimeSession.cs`
- Create: `source/DevTools.NUnit.Runtime/NUnitResultMapper.cs`
- Create: `source/DevTools.NUnit.Runtime/NUnitEventListener.cs`
- Create: `source/DevTools.NUnit.Runtime/NUnitFilterFactory.cs`
- Create: `tests/DevTools.NUnit.Runtime.Tests/DevTools.NUnit.Runtime.Tests.csproj`
- Create: `tests/DevTools.NUnit.Runtime.Tests/NUnitRuntimeSessionTests.cs`
- Create: `tests/DevTools.NUnit.Runtime.Tests/NUnitResultMapperTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `RevitDevTool.slnx`

**Consumes:** `INUnitRuntimeSession`, protocol v2 DTOs, and the Task 1 fixture.

**Produces:** `NUnitRuntimeSession`, the only production type that directly
uses NUnit framework APIs.

- [x] Reference `NUnit` 4.6.1 in Runtime only; do not reference
  `NUnit.Engine`, `NUnit.Engine.Api`, or `NUnitLite` in the production project.
- [x] Add failing discovery tests that assert the exact expanded case list,
  categories, properties, explicit/ignored state, hierarchy, and source
  locations from Task 1.
- [x] Add failing execution tests that assert lifecycle order, retry/repeat
  counts, async completion, assertion/inconclusive/error mapping, output, and
  attachment mapping.
- [x] Instantiate the public `NUnitTestAssemblyRunner`, set worker count to
  `1`, load the provided `Assembly`, and use NUnit's own filter implementation.
- [x] Map `ITest` and `ITestResult` into Core DTOs inside Runtime. Do not return
  an NUnit interface, exception, property bag, or XML node to Host.
- [x] Implement the NUnit listener so progress arrives before the final run
  response and duplicate terminal events are ignored by stable NUnit test ID.
- [x] Implement `Cancel(runId)` with NUnit's supported stop API; map cases that
  never complete to cancelled only after the framework reports/finishes stop.
- [x] Prove DevTools does not manually invoke fixture constructors, setup,
  teardown, test methods, or assertion exception types by adding an architecture
  test that rejects those patterns outside `DevTools.NUnit.Runtime`.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runtime.Tests/DevTools.NUnit.Runtime.Tests.csproj
```

Expected: the entire Task 1 matrix is discovered and run by NUnit 4.6.1 with
expected neutral results.

### Task 5: Implement the modern collectible ALC

**Files:**

- Create: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeLoadContext.cs`
- Create: `source/DevTools.NUnit.Host/Loading/ModernNUnitRuntimeSessionFactory.cs`
- Create: `tests/DevTools.NUnit.Host.Tests/ModernNUnitRuntimeSessionFactoryTests.cs`

**Produces:**

```csharp
public interface INUnitRuntimeSessionFactory
{
    INUnitRuntimeSession Create(NUnitGenerationManifest generation);
}
```

- [x] Add a failing test that loads Task 1, executes it, disposes the session,
  calls `Unload()`, drops all strong references from a `NoInlining` method, and
  verifies a `WeakReference` dies after bounded GC/finalizer cycles.
- [x] Add a test preloading a conflicting NUnit assembly into Default ALC;
  assert the generation uses its private `nunit.framework` assembly instance.
- [x] Implement one collectible ALC per generation using
  `AssemblyDependencyResolver` rooted at the shadow test assembly.
- [x] Prefer matching already-loaded Default ALC assemblies through
  `NUnitSharedAssemblyPolicy`; otherwise preserve normal CLR binding behavior.
- [x] Load runtime, NUnit, test assembly, and private dependencies from the
  generation's file-backed paths so module/PDB locations are debugger-visible.
- [x] Resolve native libraries only from the generation's manifest paths.
- [x] On dispose, detach all resolving handlers, dispose the runtime session,
  unload the ALC, and emit a retained-generation diagnostic if the weak
  reference remains alive after verification.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj -Filter ModernNUnitRuntime
```

Expected: conflicting NUnit versions coexist and the private generation is
collectible after each test.

### Task 6: Implement controlled net48 generation loading

**Files:**

- Create: `source/DevTools.NUnit.Host/Loading/NetFrameworkNUnitGeneration.cs`
- Create: `source/DevTools.NUnit.Host/Loading/NetFrameworkNUnitRuntimeSessionFactory.cs`
- Create: `source/DevTools.NUnit.Host/Loading/NUnitGenerationRegistry.cs`
- Create: `tests/DevTools.NUnit.Host.NetFramework.Tests/DevTools.NUnit.Host.NetFramework.Tests.csproj`
- Create: `tests/DevTools.NUnit.Host.NetFramework.Tests/NetFrameworkGenerationTests.cs`
- Modify: `RevitDevTool.slnx`

**Produces:** the net48 implementation of `INUnitRuntimeSessionFactory`.

- [x] Run the test process on CLR 4.8, not a net8 testhost compatibility mode.
- [x] Add a failing test that preloads one `nunit.framework` identity, then
  loads and executes Task 1 with the generation's NUnit 4.6.1 identity.
- [x] Add two real-IL generation tests proving generation one returns
  `generation-one` and generation two returns `generation-two` in the same
  AppDomain.
- [x] Add a dependency test where both generations contain the same dependency
  identity with different behavior; assert each requesting assembly receives
  the dependency registered to its own generation.
- [x] Register one scoped `AppDomain.AssemblyResolve` handler that looks up
  `ResolveEventArgs.RequestingAssembly` in `NUnitGenerationRegistry`; never
  resolve using only simple name or current output directory.
- [x] Load generation assemblies with file-backed no-context loads and register
  every loaded assembly to its immutable generation before executing user code.
- [x] Bind `DevTools.NUnit.Core` and Autodesk/shared contracts to the already
  loaded host copies; reject any generation containing a private copy that the
  loader attempts to use.
- [x] Dispose only resolver/session state. Report that managed generation count
  is retained; do not claim unload and do not delete locked generation files.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.NetFramework.Tests/DevTools.NUnit.Host.NetFramework.Tests.csproj
```

Expected: both IL generations and conflicting NUnit identities execute in one
CLR 4.8 AppDomain without cross-generation dependency binding.

### Task 7: Integrate the native runtime into the host

**Files:**

- Create: `source/DevTools.NUnit.Host/NUnitRuntimeManager.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitHost.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitHostingExtensions.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitRequestHandler.cs`
- Modify: `source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj`
- Modify: `tests/DevTools.NUnit.Host.Tests/NUnitRequestHandlerTests.cs`

**Produces:** one host service that uses the proper TFM-specific runtime
factory and serializes host test sessions.

- [x] Add failing handler tests proving discover/run/progress/cancel use a fake
  `INUnitRuntimeSessionFactory` and protocol v2.
- [x] Add a concurrency test proving a second host run returns a busy response
  or queues according to existing request policy; it must not execute two NUnit
  sessions concurrently.
- [x] Implement `NUnitRuntimeManager` with a `SemaphoreSlim(1, 1)`, active-run
  lookup by run ID, generation diagnostics, and deterministic session disposal.
- [x] Replace `NUnitReflectionRunner` DI registration with generation builder,
  TFM-specific session factory, and runtime manager registrations.
- [x] Preserve `IHostContextExecutor`: discovery and execution that may touch
  Autodesk types run on the correct host context.
- [x] Execute the Task 1 fixture twice after rebuilding the actual DLL constant;
  assert response generation IDs and observed values differ.
- [x] Keep `NUnitReflectionRunner` source temporarily for rollback, but remove
  it from DI and production selection. Task 13 deletes it after live P0/P1
  evidence; no runtime feature flag chooses between two production paths.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
```

Expected: host tests use native NUnit sessions; no production Host code invokes
NUnit lifecycle methods itself.

### Task 7A: Upgrade and publish the protocol-v2 Runner CLI

**Files:**

- Modify: `source/DevTools.NUnit.Runner/Services/NUnitPipeClient.cs`
- Modify: `source/DevTools.NUnit.Runner/Commands/DiscoverCommand.cs`
- Modify: `source/DevTools.NUnit.Runner/Commands/RunCommand.cs`
- Modify: `tests/DevTools.NUnit.Runner.Tests/`
- Modify only as required for the temporary P0 `dotnet test` bridge:
  `source/DevTools.NUnit.TestAdapter/Runner/`

**Produces:** an installed Runner CLI that speaks protocol v2 before any live
P0 gate. The temporary VSTest adapter may consume this CLI only as an internal
acceptance bridge; Task 11 still removes that adapter from the product.

- [x] Add Runner client tests for protocol-v2 hello, discover, run, progress,
  cancellation, diagnostics, attachments, and compatibility failure.
- [x] Keep `discover` and `run` as Runner-owned host operations. Neither the
  adapter nor a testhost may invoke Revit or execute NUnit tests locally.
- [x] Preserve the existing sample's real `dotnet test` path long enough to
  prove Revit API execution before MTP replaces the temporary bridge.
- [x] Publish Runner to the installed bundle and verify that the executable
  reports the expected version before Task 8.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runner.Tests/DevTools.NUnit.Runner.Tests.csproj
dotnet publish source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Release -r win-x64
```

Expected: the installed Runner can drive protocol-v2 discovery and execution;
P0 live verification does not depend on the later MTP `serve` endpoint.

### Task 8: Pass the live P0 host matrix

**Files:**

- Create: `docs/agents/nunit-native-runtime-verification.md`
- Update progress/evidence in this plan after each run.

Evidence pack (2026-08-12): `docs/agents/nunit-native-runtime-verification.md` +
`%LOCALAPPDATA%\RevitDevTool\task8-evidence\`. **Not fully closed** â€” see verdict.

- [x] Revit 2023: let Runner locate/reuse or launch Revit with the installed
  Dynamo add-in loaded; do not start Revit directly. Record the selected PID
  and all loaded assemblies whose simple name is `nunit.framework`, including
  full identity and location.
  _(PID 45108; Dynamo cores loaded; AD has DevTools 4.6 generation copies;
  Dynamo 2.6.3 on disk under `AddIns\DynamoForRevit`, not loaded this session.)_
- [x] Revit 2023: discover and run the Task 1 net48 fixture; record expanded
  cases, lifecycle log, result summary, generation manifest, and host PID.
  _(Full matrix incl. async: 27/2/1/1; async probe 2/2 Passed after off-UI Run.)_
- [x] Revit 2023: rebuild to generation two without restarting Revit and prove
  new IL plus generation-specific dependency behavior.
  _(gen2 marker on same PID; new `generation_id`.)_
- [~] Revit 2025: repeat discovery/run/rebuild. Isolation (new id + new IL) is
  the gate; live `generation.unloaded` is not required (ADR 0016 §4).
  _(Operator proxy: Revit 2026 net8. Switch OK; live reports `generation.retained`.)_
- [x] Revit 2027: compile the net10 host path. Live run only when a 2027 host is
  installed; missing 2027 is an env blocker, not a P0 fail.
- [x] Repeat the supported smoke subset in AutoCAD for one modern host when
  installed (Civil 3D 2026 Runner smoke Passed). Plain AutoCAD is optional.
- [x] Run at least one real `dotnet test` host smoke that executes Revit API
  logic in the selected Revit PID. The default P0 bridge is the passing smoke
  case in `samples/DevTools.NUnit.SampleTests`; exclude its deliberate demo
  failure from the acceptance invocation.
  _(Arithmetic Passed, `host-pid=45108`; adapter still ran siblings â€” filter gap.)_
- [x] Before each deploy, kill Revit and rebuild the add-in for the target year.
  After deployment, allow only Runner to establish the live session. Reuse that
  session for generation-two proof; do not kill it merely to rebuild a test
  assembly.
  _(2023 deploy earlier same day; gen-two reused PID 45108.)_
- [x] Stop P1 if Revit 2023 plus Dynamo or Revit 2025 isolation fails. Live ALC
  collection is waived. Never expand reflection emulation.

Required compile proof follows `.agents/skills/build/SKILL.md`:

```powershell
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2027 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Required deploy and live smoke pattern for each installed host year:

```powershell
scripts/kill-host.ps1 -HostApp Revit -Year 2023
scripts/build-host.ps1 -Year 2023
dotnet publish source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Release -r win-x64
dotnet test samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2023 --filter "FullyQualifiedName~Arithmetic_runs_inside_host"
```

The exact smoke project or filter may change as MTP replaces the temporary
adapter, but the evidence must remain a real `dotnet test`, a Runner-selected
Revit PID, and observed Revit API behavior inside that process.

P0 exit gate: real NUnit owns the complete Task 1 matrix, Revit 2023/Dynamo
coexists, modern hosts isolate a rebuilt generation without restart, and a true
IL rebuild runs without restarting the modern host. Live `generation.unloaded`
is not required. Live evidence includes an actual Revit API smoke through
`dotnet test` or Runner; missing Revit 2027 is compile-only.

### Remaining gaps (carry into next session)

Tracked evidence: [`docs/agents/nunit-native-runtime-verification.md`](../../agents/nunit-native-runtime-verification.md).

| Gap | Severity | Notes / next action |
|-----|----------|---------------------|
| Modern ALC reports `generation.retained` in live Revit | Accepted | ADR 0016 §4: isolation is the gate. Do not chase unload. |
| Adapter/`dotnet test` filter runs sibling sample tests | P1 | Replaced by MTP selected-run mapping; delete adapter after MTP sample green. |
| Revit 2027 live | Env blocker | Compile net10 host + existing Host.Tests. Live when installed. |

P1 is **unblocked**. Implement the thin MTP package first (`DevTools.NUnit`). Defer `Runner serve` (Task 9) until a long-lived IDE session needs it.
## P1 â€” MTP-Only IDE Integration

### Task 9: Make Runner the explicit host-process owner

**Deferred.** VSTest replacement does not need a new `Runner serve` protocol.
The MTP package starts the installed Runner CLI one-shot (same as the adapter).
Revisit only if Test Explorer needs a long-lived Runner session.

### Task 10: Add the MTP 2.3.2 consumer package (`DevTools.NUnit`)

**Files:**

- Create: `source/DevTools.NUnit.Core/Contracts/NUnitRunnerMessages.cs`
- Create: `source/DevTools.NUnit.Runner/Services/IHostSessionController.cs`
- Modify: `source/DevTools.NUnit.Runner/Services/HostSession.cs`
- Modify: `source/DevTools.NUnit.Runner/Services/HostLocator.cs`
- Create: `source/DevTools.NUnit.Runner/Commands/ServeCommand.cs`
- Modify: `source/DevTools.NUnit.Runner/Program.cs`
- Create: `tests/DevTools.NUnit.Runner.Tests/HostSessionControllerTests.cs`
- Create: `tests/DevTools.NUnit.Runner.Tests/ServeCommandTests.cs`

**Produces:** a Runner-owned controller endpoint used by MTP test sessions.
`DevTools.NUnit.Runner serve` is the activation endpoint. The NUnit consumer
package supplies a separate MSBuild/launcher hook that starts or reuses this
endpoint before the MTP test application connects. `DevTools.Daemon` remains
the MCP process and does not own NUnit test-runner activation. MTP only connects
to the resulting endpoint and reports a clear unavailable result when it is
absent.

```csharp
public sealed record NUnitRunnerRequest(
    string RequestId,
    string Method,
    JsonElement? Parameters);

public sealed record NUnitRunnerEvent(
    string RequestId,
    string Kind,
    JsonElement? Payload,
    NUnitRunnerHostInfo? Host);

public sealed record NUnitRunnerHostInfo(
    string Host,
    string Version,
    int ProcessId,
    bool IsOwnedByRunner);
```

- [ ] Add failing tests for reuse of an existing host, launch of a missing host,
  cancellation, Runner-owned host exit, reused-host disconnect, and Runner
  shutdown.
- [ ] Make `HostSession` record whether Runner launched the Autodesk process or
  merely connected to a pre-existing process.
- [ ] Permit termination only for a Runner-owned host and only under the
  existing explicit launch/cancellation policy. Never terminate a reused host.
- [ ] Add a framed Runner endpoint for hello, discover, run, cancel, and
  shutdown. Forward host progress without converting it to console prose.
- [ ] Record the package launcher â†’ `Runner serve` boundary in the architecture
  document. Prove that MTP can neither start nor stop the endpoint and that
  endpoint shutdown policy stays with Runner and its external launcher.
- [ ] Include authoritative Autodesk host PID and ownership in every session
  start event for diagnostics and future optional IDE-specific debugging.
- [ ] Keep CLI discover/run commands as thin clients of the same
  `IHostSessionController`; do not duplicate host discovery policy.

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runner.Tests/DevTools.NUnit.Runner.Tests.csproj
```

Expected: Runner alone contains Autodesk host process policy, and its endpoint
can stream the same Core discovery/run events used by the host pipe without
giving MTP process-lifecycle authority.

### Task 10: Add the MTP 2.3.2 compatibility extension

**Files:**

- Create: `source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj`
- Create: `source/DevTools.NUnit.Mtp/DevToolsNUnitFramework.cs`
- Create: `source/DevTools.NUnit.Mtp/DevToolsNUnitFrameworkCapabilities.cs`
- Create: `source/DevTools.NUnit.Mtp/DevToolsNUnitBuilderHook.cs`
- Create: `source/DevTools.NUnit.Mtp/DevToolsNUnitMessageMapper.cs`
- Create: `source/DevTools.NUnit.Mtp/RunnerTransportClient.cs`
- Create: `source/DevTools.NUnit.Mtp/build/DevTools.NUnit.Mtp.props`
- Create: `tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj`
- Create: `tests/DevTools.NUnit.Mtp.Tests/DevToolsNUnitFrameworkTests.cs`
- Create: `samples/DevTools.NUnit.Mtp.SampleTests/DevTools.NUnit.Mtp.SampleTests.csproj`
- Create: `samples/DevTools.NUnit.Mtp.SampleTests/global.json`
- Modify: `Directory.Packages.props`
- Modify: `RevitDevTool.slnx`

**Produces:** one auto-registered MTP `ITestFramework` that connects to an
already activated Runner endpoint, delegates all process and Autodesk host
policy to Runner infrastructure, and never executes NUnit locally.

- [ ] Pin `Microsoft.Testing.Platform` and
  `Microsoft.Testing.Platform.MSBuild` 2.3.2 centrally.
- [ ] First compile the smallest official `ITestFramework` example and inspect
  generated `SelfRegisteredExtensions.g.cs`; record exact registration output
  in the test artifact so API drift fails visibly.
- [ ] Add fake-Runner tests for discover, selected run, filter run, pass,
  failure, skip, error, cancellation, output, attachments, no compatible host,
  and protocol mismatch.
- [ ] Implement MTP `ITestFramework.CreateTestSessionAsync`,
  `ExecuteRequestAsync`, and `CloseTestSessionAsync`; translate only between
  MTP messages and Core/Runner DTOs.
- [ ] Open and close only an MTP-to-Runner transport session. Do not create,
  terminate, monitor as a child, or otherwise own the Runner process. If the
  endpoint is absent, publish an actionable infrastructure error.
- [ ] Report Task 2 stable IDs, hierarchy, traits, source locations, output,
  attachments, duration, error messages, and stack traces to the MTP message
  bus.
- [ ] Register through `TestingPlatformBuilderHook` so a consuming SDK-style
  project does not need a hand-written `Program.cs`.
- [ ] Make host-test opt-in explicit in package props and fail the build when
  an ordinary NUnit MTP runner, MTP VSTest Bridge, or `NUnit3TestAdapter` would
  also claim the same assembly. Do not silently allow two framework owners.
- [ ] Add an architecture test that rejects references from
  `DevTools.NUnit.Mtp` to `HostLocator`, `HostSession`, Autodesk executable
  names, `Process.Start`, process enumeration/kill APIs, service-control APIs,
  or any debugger API. `RunnerTransportClient` may own only its transport
  connection, never a process.
- [ ] Build the sample as an executable and prove `dotnet test` uses MTP with a
  scoped `global.json` runner selection. Use a .NET 10 SDK and place
  the SDK policy plus MTP runner selection in the sample directory, not the
  repository root, because repository test projects cannot mix VSTest and MTP
  under one root selection.

Scoped `global.json`:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMinor"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Run:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj
Push-Location samples/DevTools.NUnit.Mtp.SampleTests
dotnet test DevTools.NUnit.Mtp.SampleTests.csproj
Pop-Location
```

Expected: MTP discovers and executes only through the fake/live DevTools
Runner; the sample never loads RevitAPI or NUnit tests into the MTP process.

### Task 11: Remove the experimental VSTest surface

**Files:**

- Delete: `source/DevTools.NUnit.TestAdapter/`
- Delete: `tests/DevTools.NUnit.TestAdapter.Tests/`
- Modify: `samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj`
- Modify: `samples/Sample.slnx`
- Modify: `RevitDevTool.slnx`
- Modify: `Directory.Packages.props`

- [ ] Require Task 10 fake-Runner and MTP sample gates to pass before deleting
  the adapter projects.
- [ ] Remove `ITestDiscoverer`, `ITestExecutor`, VSTest mapper, local reflection
  discoverer, executor URI, adapter props/targets, and ILRepack adapter work.
- [ ] Remove the `NUnit3TestAdapter` central version only after `rg` proves no
  remaining project consumes it.
- [ ] Convert the current sample to the MTP package/build hook or replace it
  with `DevTools.NUnit.Mtp.SampleTests`; do not leave two host-test samples with
  different owners.
- [ ] Add a repository architecture test that fails when a production project
  references `Microsoft.TestPlatform.ObjectModel`,
  `Microsoft.Testing.Extensions.VSTestBridge`, `ITestDiscoverer`, or
  `ITestExecutor`.

Run:

```powershell
rg -n "Microsoft.TestPlatform.ObjectModel|VSTestBridge|ITestDiscoverer|ITestExecutor|NUnit3TestAdapter" source tests samples Directory.Packages.props
dotnet build RevitDevTool.slnx -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Expected: the search returns no production VSTest integration and the solution
builds with MTP as the only IDE test-platform project.

### Task 12: Pass the IDE discovery/run matrix

**Files:**

- Create: `docs/agents/nunit-ide-verification.md`
- Modify the sample only when an IDE requires checked-in project settings;
  keep personal `.idea`, `.vs`, and workspace state untracked.

- [ ] Visual Studio 2022 17.12 or newer: enable MTP server mode, build the MTP
  sample, and verify discovery/run/filter/rerun/output in the intended Revit
  PID.
- [ ] Rider 2026.1 or newer: enable Testing Platform discovery, verify the node
  is owned by the MTP custom framework rather than
  `NUnitTestRunnerRunStrategy`, then run inside Revit.
- [ ] C# Dev Kit: use only the SDK-style Revit 2025+ sample, verify MTP Test
  Explorer discovery/run. Record net48 project/debugger limitations separately
  rather than turning them into a VSTest fallback.
- [ ] For each IDE capture version, provider/strategy name, host PID,
  generation ID, Runner PID, final test outcome, output, and cancellation
  behavior.
- [ ] Do not claim an IDE supported when it merely displays test nodes. The
  execution log must prove the test ran in the intended Autodesk host PID.

P1 exit gate: Visual Studio MTP run passes on Revit 2024 and 2025, Rider MTP run
passes without native-provider hijack, C# Dev Kit MTP run passes on Revit 2025,
and logs prove MTP delegated process policy to Runner in every case.

## P2 â€” Packaging And Optional Ergonomics

### Task 13: Package one consumer surface and retire the emulator

**Files:**

- Modify: `source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj`
- Modify: `source/DevTools.NUnit.Mtp/build/DevTools.NUnit.Mtp.props`
- Modify: `source/DevTools.NUnit.Mtp/build/DevTools.NUnit.Mtp.targets`
- Modify: `build/Modules/*` files that currently publish/bundle NUnit Runner.
- Modify: `docs/product/nunit-host-testing.md`
- Modify: `docs/agents/nunit-host-testing.md`
- Modify: this plan and the historical plan status.

- [ ] Pack one public `DevTools.NUnit` consumer package from the MTP project.
  Include MTP registration assets, Runner endpoint configuration, and the
  external `Runner serve` activation hook without exposing Host or Runtime as
  compile-time consumer APIs.
- [ ] Make the activation hook visibly separate from the MTP extension. The
  package configures the installed Runner launcher, but no MTP type may call
  process or service lifecycle APIs and the MCP Daemon is not involved.
- [ ] Copy `DevTools.NUnit.Runtime.dll`, NUnit 4.6.1, and required symbols into
  the installed add-in runtime layout exactly once per supported TFM.
- [ ] Add package-layout tests that fail on duplicate framework/runtime DLLs,
  missing builder hooks, or compile references to private runtime assemblies.
- [ ] Restore a clean sample from the produced local nupkg, activate Runner
  through the selected non-MTP owner, and run MTP against a fake Runner before
  any publication.
- [x] Delete `NUnitReflectionRunner`, attribute-name helpers, and tests that
  validate emulated lifecycle semantics after P0/P1 evidence is recorded.
- [ ] Remove unused `NUnit.Engine` and `NUnit.Engine.Api` central package pins
  only after `rg` proves no project reference remains.
- [ ] Update product docs with behavior that actually passed. Keep failed or
  unavailable IDE/host combinations in limitations.
- [ ] Confirm the 2026-08-10 plan was marked superseded when this replacement
  plan was accepted, preserving its completed historical scopes and evidence.

Required proof:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Core.Tests/DevTools.NUnit.Core.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runtime.Tests/DevTools.NUnit.Runtime.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj
scripts/pack.ps1
```

Expected: one package, no reflection execution path, no NUnit Engine dependency,
and clean sample restore/run evidence.

### Task 14: Add optional debugger integration per IDE

**Files:**

- Optional create: a separate Visual Studio integration project using supported
  Visual Studio interop.
- Optional create: a separate Rider plugin/integration project using the Rider
  SDK.
- Optional create: a separate VS Code extension/integration project for C# Dev
  Kit-compatible modern hosts.

- [ ] Start only after P0, MTP packaging, and the IDE discovery/run matrix pass.
  A missing automatic attach path does not block the NUnit/MTP release.
- [ ] Implement each IDE independently: request `ensure/locate host` from
  Runner, receive the authoritative host PID, attach through that IDE's API,
  wait for that IDE to confirm attach, then submit the ordinary MTP run.
- [ ] Keep all IDE SDK and debugger interop dependencies in the corresponding
  optional integration project. Do not add debugger DTOs, attach methods, or a
  debug-ready/continue state machine to Core, Host, Runtime, Runner protocol,
  or MTP.
- [ ] Retain manual attach as the supported fallback wherever an IDE has no
  stable automation API. C# Dev Kit support remains limited to the TFMs its
  debugger actually supports.
- [ ] Test an integration against its IDE API and verify the debugger attached
  to the Runner-reported Autodesk PID before the normal run begins. Do not use
  a fake cross-IDE attach contract as acceptance evidence.
- [ ] Evaluate source generation only by a benchmark comparing post-build MTP
  discovery/source navigation with and without generated static metadata. Do
  not ship a generator unless it improves a measured IDE problem and preserves
  runtime discovery authority.

## Risks And Recovery

- **NUnit public API changes across 4.x.** First release pins 4.6.1 and validates
  the runtime assembly version before loading. Supporting a range requires a
  new compatibility matrix, not binding redirects.
- **net48 resolver reentrancy or generation cross-wire.** Registry lookup is by
  requesting assembly and immutable generation. On any ambiguous request, fail
  with identities and paths instead of returning the latest simple-name match.
- **ALC remains alive after unload.** Emit the retained roots known to
  DevTools, keep the generation diagnostic, and block the modern P0 gate.
- **NUnit worker invokes tests off the Autodesk context.** Force one worker and
  run the entire framework session within `IHostContextExecutor`; capture thread
  ID in the acceptance fixture and fail the gate on drift.
- **MTP conflicts with official NUnit runner.** Host-test projects explicitly
  opt into one framework owner; package targets produce a build error when both
  owners are present.
- **Rider still selects native NUnit.** Treat MTP provider identity as an
  acceptance assertion. Do not add more adapter-layout workarounds or a Rider
  SDK dependency before analyzing Rider logs.
- **MTP accidentally becomes a process orchestrator.** Architecture tests ban
  process/service lifecycle APIs from the MTP project. End-to-end logs must
  identify the external Runner activation owner and prove MTP owns only its
  transport connection.
- **An IDE-specific debugger attaches to the wrong process.** That integration
  must use the authoritative PID returned by Runner and wait for the IDE's own
  attach confirmation before issuing the ordinary run. Failure affects only
  that optional IDE integration, not the Core/MTP contract.
- **Shadow generation temp growth.** Do not delete loaded paths. Add startup
  cleanup later using process ownership and age only after runtime correctness.
- **Rollback before P0 passes.** Keep current reflection runner available only
  on the pre-change revision. Do not maintain a runtime feature flag after
  native acceptance; revert the coherent implementation group if needed.

## Progress

- [x] ADR 0016 accepted.
- [x] Prioritized execution plan drafted for review.
- [x] Plan accepted.
- [x] On acceptance, mark the 2026-08-10 active plan superseded.
- [x] Tasks 0â€“7A implemented in tree (Core/Transport, Runtime, generation
  loading, Host manager, Runner CLI v2, packaging ownership).
- [x] Task 8 live host matrix — isolation + Task1 + Civil3D smoke; live unload waived.
- [x] P0 exit gate complete (isolation, not live ALC collection).
- [x] P1 thin MTP package `DevTools.NUnit` (mapper + Runner CLI + hook) — in tree;
  fake-Runner tests green; samples converted to MTP (`global.json` + Exe).
- [x] Live MTP sample `dotnet test` on Revit 2026 (`Arithmetic_runs_inside_host` Passed).
- [ ] Task 11: delete unpublished VSTest adapter.
- [ ] P1 MTP-only compatibility complete.
- [ ] P1 IDE matrix complete.
- [ ] P2 packaging and documentation complete.
- [ ] P2 optional per-IDE debugger integrations evaluated independently.

## Validation

- Focused proof: every task names its project/filter and expected observation.
- Contract proof: protocol v2 golden JSON, MTP mapping tests, and an
  architecture test proving MTP has no process/debugger ownership APIs.
- Runtime proof: full NUnit semantics fixture, real IL generation change,
  dependency-version change, conflicting NUnit identity, and modern ALC unload.
- Live proof: Revit 2023 plus Dynamo recorded; Revit 2025 substituted by
  Revit 2026 (net8) per operator â€” unload still `generation.retained`.
- Operational live proof: agents use Runner rather than directly starting
  Revit, keep the live lane serialized, record the selected host PID, and kill
  the host before deploying changed add-in binaries.
- Deployment proof: build `RevitDevTool` after Core/Host/Runtime changes and
  publish the installed Runner after Runner/Core protocol changes.
- End-to-end proof: at least one real `dotnet test` invocation exercises Revit
  API behavior in the Runner-selected process. The exact smoke assembly may
  evolve from the temporary adapter sample to the MTP sample during migration.
- IDE proof: provider ownership plus actual Autodesk host PID, not discovery UI
  alone.
- Optional debugger proof: the IDE confirms attachment to the Runner-reported
  Autodesk PID before an unchanged normal run request is sent.
- Repository proof: compile Autodesk 2022, 2025, and 2027 using the build skill;
  package only after all P0/P1 gates are recorded.

## Result

P0 code path is in the working tree and largely proven live on Revit 2023
(including async). Task 8 remains open only on live modern ALC unload
(`generation.retained` in Revit 2026; host unit tests unload). Continuation
should prioritize retain-root diagnosis in-process, then decide whether to waive
2027/AutoCAD as env blockers before P1.
