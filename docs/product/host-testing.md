# MTP host testing

RevitDevTool runs tests inside Revit, AutoCAD, and Civil 3D through
Microsoft.Testing.Platform. `RevitDevTool.TestAdapter` is the only public test
integration package. NUnit is the default engine; TUnit is an opt-in provider
([tunit-host-testing.md](tunit-host-testing.md)). The VSTest adapter and
NUnit-specific bridge protocol are not part of the supported product on
`develop`; their final baseline is retained on branch `testing/nunit-vstest`.

## Test project contract

- Reference `RevitDevTool.TestAdapter` (depends on
  `Microsoft.Testing.Platform.MSBuild` 2.4.0). Framework package (default NUnit
  4.6.1) is a local choice, not a package dependency. Package props set
  `OutputType=Exe`. Declare `HostName`, `HostVersion`, optional `ForceLaunch`,
  `PerTestTimeout`, `LaunchTimeout`. `ForceLaunch=true` always starts a new host
  (skip reuse). `PerTestTimeout` is the per-test budget after the host is ready; `testing/run`
  pipe wait = budget × test count. `LaunchTimeout` waits for the host pipe after
  process start. `TestingFramework` (`nunit` default, `tunit` opt-in) overrides
  the in-host engine without changing the NuGet.
- `testconfig.json` is generated from csproj properties; incremental build
  refreshes `[AssemblyName].testconfig.json` (no Rebuild after `HostName`,
  `ForceLaunch`, `PerTestTimeout`, or `LaunchTimeout` changes).
  `Microsoft.Testing.Platform.MSBuild` copies the same file. Adapter reads
  `devtools` via MTP `IConfiguration` (same as `mstest` / `xUnit`). Author
  `testconfig.json` beside the csproj for `platformOptions`; no `.runsettings`.
- `dotnet test` needs `"test": { "runner": "Microsoft.Testing.Platform" }` in
  `global.json`. Put that runner on the **repo root** when every `dotnet test`
  project in the tree is MTP, or when the only VSTest leftover is isolated with
  a nested `"runner": "VSTest"` (this repo: `samples/ricaun.NUnit.SampleTests`).
  Scope MTP next to the test project only when many VSTest/`dotnet test` projects
  still share the tree. Nearest `global.json` replaces the whole file (include
  `sdk` in the nested file). `dotnet test` from a folder uses that cwd's file,
  not the `--project` path.
- On `net48` the consumer must add `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`
  (the test project is an `Exe` and restore does not read a RID from
  `build/*.props`). A `net48` TUnit project needs nothing else — the package
  supplies the `[ModuleInitializer]` attribute TUnit generates against.
  Details: [tunit-host-testing.md](tunit-host-testing.md).
- Packaged modern TFMs copy private `build/runtime`: exact
  `net{version}-windows7.0` when present, else nearest lower shipped folder
  (`net9` → `net8.0-windows7.0`). A consumer TFM with no match fails the test
  project build instead of copying nothing.

## Discovery

- Host-free IDE discovery: `DevTools.NUnit.MTP` + NUnit `ExploreTests` when the
  assembly loads in the MTP process. Host API refs (`Revit_All_Main_Versions_API_x64`
  for Revit) are compile-only (`Copy Local false`). Build writes
  `$(TargetName).discovery-refs.txt` from compile-only NuGet `ReferencePath`;
  testhost resolves via `AssemblyResolve` (API DLLs not beside the exe). With
  that file, discovery loads an isolated copy of the test assembly and resolves
  those paths — no host. If `ExploreTests` cannot build a tree, discovery fails
  with that NUnit reason; no PE metadata list.
- `NUnitTolerantAssemblyBuilder` uses types that did load instead of marking the
  whole assembly `NotRunnable` (testhost uses the same builder → assembly-level
  attributes and sort order match the host). Sets `TestContext.WorkDirectory`
  (generation shadow); early `WorkDirectory` access throws. net48 has no load
  context: same-identity product assemblies bind to the host copy, not the
  generation snapshot — restart the host after deploying a matching add-in, or
  use net8+ (`AssemblyLoadContext`).

## Identity and filters

- Protocol split: platform `TestNode` (IDE) vs `testing/*` (host). NUnit owns
  discovery tree, identity, filter XML, skip/explicit, parameterized-case naming.
- UID / wire `TestId` / host `<test>`: NUnit `ITest.FullName`. TestNode uid
  matches except `[TestCase(TestName=)]` / `SetName` → `Class.Method("DisplayName")`
  (avoids indexing `Class.Unit_X` beside `Named_basis_length_is_one`).
  `TestMethodIdentifierProperty.TypeName`: C# source type, no namespace, no
  fixture ctor args, no closed generic args, no ECMA-335 backtick arity. Visual
  Studio binds syntax: ``GenericClosedTests`1`` and `GenericClosedTests<Int32>`
  → "No source available". PDB uses metadata names (`GenericClosedTests`1`,
  nested `+`); closed generic args stay on uid / `FullName`. Display types in
  `TypeName` break IDE hierarchy on `.` in ctor args. `TestFileLocationProperty`
  is the line-accurate fallback when the identifier bind is enough to open the
  file.
- Filter: UID / `--filter-uid` / Test Explorer send the TestNode uid. NotRunnable stub
  `Class.Method` when `[TestFixtureSource]` / `[TestCaseSource]` cannot expand
  (Revit types at load) keeps that uid. Host filter matches
  `Class("args").Method` and `TestName` / `SetName` children (`ITest.FullName`);
  results fold back onto the requested UID and onto discovered leaf UIDs. Unfiltered runs (no `--filter` /
  `--filter-uid`) remap `FullName` onto discovered uids; names-only `--filter` keeps per-leaf host identities; `(args)`
  ids stay exact `<test>`. Result nodes reuse discovered `TestMethodIdentifier`
  (C# method name). `--filter` / `Name=` → `Names` / `<name re="1">`.
  `--filter-uid`: json TestNode uid (`ITest.FullName`, or
  `Class.Method("DisplayName")` for `TestName` / `SetName`).
  PowerShell: quote `"` in uids (`--filter-uid 'Ns.Class.Method("Unit_X")'`).
  Unreported uids publish as Failed (same identity) instead of dropped. MTP
  `TestFrameworkCapabilities` stay empty (no VSTest-bridge extras).

## Run

- Host launch/reuse only after execution request (`testing/hello`, `testing/run`,
  `testing/cancel`). Cancel or launch timeout kills only a host this run spawned
  (not a reused instance). Visual Studio Stop Debugging during that wait cancels
  when the testhost PID (`--debug-parent-pid`) exits, then kills the in-flight
  host. Cancel while a breakpoint is hit in the reused host does not free the
  idle thread until you Continue (or detach). The next `testing/hello` starts a
  new session; it does not inherit a poisoned cancel from the dropped client.
- Visual Studio Test Explorer **Debug** attaches the testhost; Runner then
  EnvDTE-attaches that Visual Studio instance to the Autodesk host
  ([0025](../decisions/0025-runner-owned-visual-studio-host-attach.md)). Rider
  and C# Dev Kit attach the host PID and **Run** (attach does not block test
  execute). VS Code/forks and PyCharm attach Python via `debugpy` port 5678
  (`.vscode/launch.json`, `.run/Attach.run.xml`).

## Ownership

| Module | Responsibility |
|---|---|
| `DevTools.Testing.Abstractions` | Neutral run/result/runtime contracts, plus the testhost discovery plug-in (`IHostTestDiscoverer`). MTP compiles against this assembly, not `DevTools.TestAdapter` |
| `DevTools.Testing.Transport` | `testing/*` JSON, pipe methods, and TestRunner process client |
| `DevTools.Testing.Host` | In-host `testing/*` handler, generation store, and runtime-session lifecycle |
| `DevTools.TestAdapter` | Published `RevitDevTool.TestAdapter`. MTP control plane (command line, host launch request, TestNode publish). Copies `DevTools.NUnit.MTP.dll` next to the test exe. Does not parse NUnit names |
| `DevTools.NUnit.MTP` | Authoritative local discovery (`NUnitTestAssemblyRunner` + `ExploreTests`), metadata `TypeName`, DisplayName suffix, host filter XML, and result fold. Loaded beside the adapter; not ILRepacked into it |
| `DevTools.NUnit.Runtime` | Default in-host engine: NUnit execution inside an isolated generation |
| `DevTools.NUnit.Host` | NUnit closure/version policy, Dynamo-safe framework sharing, isolated runtime activation, and `TestingSelection` → NUnit filter XML |
| `DevTools.TestRunner.Core` | Framework-neutral host locate/launch/reuse, debugger attach, and `testing/*` pipe client |
| `DevTools.TestRunner` | Southbound executable: locate/launch the host and send `testing/run`. Framework id is a CLI option from the adapter `devtools` section |

The cross-load-context identity is `DevTools.Testing.Abstractions`. Runtime
payloads do not carry a provider-specific transport assembly. Supported runtime
targets are net48, net8, and net10; the former `netstandard2.0` compatibility
target is removed.

## Samples and verification

Samples: `samples/DevTools.NUnit.SampleTests` (Revit),
`samples/DevTools.NUnit.Civil3D.SampleTests` (Civil 3D). Those samples still
use NUnit attributes because NUnit is the default engine.
`samples/ricaun.NUnit.SampleTests` is a comparison sample: it links the same
`HostSmokeTests` and runs them through `ricaun.RevitTest.TestAdapter` (VSTest).
It is not the product contract — do not use it as the verify path, and do not
try to make it MTP.

Run the generated test executable or use the Microsoft.Testing.Platform
`dotnet test`/IDE surface. This repo puts the MTP runner on the root
`global.json`; `samples/ricaun.NUnit.SampleTests` overrides with `"runner":
"VSTest"` and must be run from that folder. The adapter copies
`DevTools.NUnit.MTP.dll` next to the test exe. Consumers reference NUnit; they
do not add `DevTools.NUnit.MTP` as a ProjectReference.

Use an Autodesk configuration (`Debug.Autodesk.2024`, `Release.Autodesk.2024`,
…). Plain `Debug` / `Release` do not set `RevitVersion` / `TargetFramework`;
the sample does not build, and Test Explorer then shows a source/method tree
that is not MTP `ExploreTests`.

Canonical `samples/DevTools.NUnit.SampleTests` discovery: test exe
`--list-tests json` leaf count. Measured **70** for `Debug.Autodesk.2024` and
`Release.Autodesk.2024` (same UIDs):

| Fixture | Leaves |
|---|---|
| `BoundingBoxXyzSampleTests` | 26 |
| `ValueSourceTests` | 21 |
| `TestCaseTests` | 8 |
| `LifecycleTests` | 4 |
| `HostSmokeTests` | 3 |
| `NamedFixtureSourceTests("alpha.rvt"\|"beta.rvt")` | 2 |
| stubs (`Box_source`, `Span_is_one`, `Wall_type_id`) | 3 |
| `InheritedGeometryTests`, `Nested+Inner`, `GenericClosedTests<Int32>` | 1 each |
| `GenericRevitTypeTests<XYZ\|BoundingBoxXYZ>` | **0** (not ExploreTests) |

Test Explorer counts are not that leaf count. Visual Studio Real-Time Discovery
adds `[TestCase(TestName=…)]` children that never match MTP uids
(`TestCaseTests`: 8 + 3 = 11 under `Named_basis_length_is_one`; `.SetName`
cases do not get those extras). Turn off **Tools → Options → Test → Discover
tests in real time from C# and Visual Basic .NET source files**, then refresh.
NUnit documents the same gap ([nunit3-vs-adapter#1256](https://github.com/nunit/nunit3-vs-adapter/issues/1256),
[#489](https://github.com/nunit/nunit3-vs-adapter/issues/489)); adapter code
cannot dedupe RTD nodes. A ~32-node tree (methods, `GenericRevitTypeTests`,
`TestName` leaves as extra methods) is grouping/source discovery, not the
70-leaf CLI list; runs from it do not send expanded FullName UIDs.

## Packaging

Two artifacts. Installer workflow does not publish NuGet; adapter workflow does
not pack the host bundle.

- **NuGet** `RevitDevTool.TestAdapter` — `scripts/pack-test-adapter.ps1` /
  `PublishTestAdapter.yml`. Version is `<Version>` in
  `source/DevTools.TestAdapter/DevTools.TestAdapter.csproj`. Modern targets keep
  implementation assemblies in a private `build/runtime` closure. net48
  ILRepacks the adapter except `DevTools.Testing.Abstractions.dll`, which stays
  beside the test exe (with `DevTools.NUnit.MTP.dll`) so testhost discovery
  shares one `IHostTestDiscoverer` / `HostTestDiscovery` identity. Consumers see
  only the platform adapter compile surface.
- **Installer / bundle** — `scripts/pack.ps1` / `PublishRelease.yml`. Ships
  `DevTools.TestRunner.exe` and the in-host testing stack
  (`Testing.Host`, `NUnit.Host`, `NUnit.Runtime`). Required for live runs;
  the NuGet does not replace it.

Pack graph and restore constraints:
[architecture/Testing](../architecture/Testing/README.md). Platform-only
boundary: [0022](../decisions/0022-nunit-mtp-only-testing-stack.md). Kernel
split: [0021](../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md).
