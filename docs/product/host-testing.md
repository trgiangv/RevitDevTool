# MTP host testing

RevitDevTool runs tests inside Revit, AutoCAD, and Civil 3D through
Microsoft.Testing.Platform. `RevitDevTool.TestAdapter` is the only public test
integration package. NUnit is the default engine; TUnit is an opt-in provider
([tunit-host-testing.md](tunit-host-testing.md)). The VSTest adapter and
NUnit-specific bridge protocol are not part of the supported product on
`develop`; their final baseline is retained on branch `testing/nunit-vstest`.

## User contract

- Test projects reference `RevitDevTool.TestAdapter` and
  `Microsoft.Testing.Platform.MSBuild`. The test framework package (default
  NUnit 4.6.1) is a local project choice, not a package dependency.
- Test projects are executable Microsoft.Testing.Platform applications
  (`OutputType=Exe`) and declare `HostName`, `HostVersion`, and optional
  `ForceLaunch`, `PerTestTimeout`, and `LaunchTimeout`.
  `ForceLaunch=true` always starts a new host (skip reuse).
  `PerTestTimeout` is the per-test budget after the host is ready;
  the `testing/run` pipe wait is this times the number of tests in that run.
  `LaunchTimeout` is the wait for the host pipe after process start.
  Cancel or launch timeout kills only a host this run spawned (not a reused
  instance). Visual Studio Stop Debugging during that wait cancels when the
  testhost PID (`--debug-parent-pid`) exits, then kills the in-flight host.
  Cancel while a breakpoint is hit in the reused host does not free the idle
  thread until you Continue (or detach). The next `testing/hello` starts a
  new session; it does not inherit a poisoned cancel from the dropped client.
- Override `TestingFramework` in the test
  csproj to change the in-host engine without changing the NuGet. Default
  engine is NUnit (`TestingFramework=nunit`).
- Host options are generated as `testconfig.json` from the csproj properties.
  An incremental build refreshes `[AssemblyName].testconfig.json`; Rebuild is
  not required after changing `HostName`, `ForceLaunch`, `PerTestTimeout`, or
  `LaunchTimeout`. Microsoft.Testing.Platform.MSBuild copies the same file.
  The adapter reads the `devtools` section
  through MTP `IConfiguration` (same pattern as `mstest` / `xUnit`). Author
  `testconfig.json` beside the csproj to add `platformOptions`; do not use
  `.runsettings`.
- IDE discovery is host-free. `DevTools.NUnit.MTP` uses NUnit
  `ExploreTests` when the assembly can load in the MTP process. Autodesk API
  packages (`Revit_All_Main_Versions_API_x64`, `AutoCAD.NET`, Nice3point
  `ref/` packs) are compile-only (Copy Local false). After Build the adapter
  writes `$(TargetName).discovery-refs.txt` from compile-only NuGet
  `ReferencePath` (Copy Local false). Testhost loads those paths via
  `AssemblyResolve`; API DLLs are not copied next to the test exe. When that file is present, discovery loads an isolated copy of the test
  assembly and resolves those paths — no host process. If ExploreTests cannot
  build a tree, discovery fails with that NUnit reason. There is no PE metadata
  list. In-host NUnit load is also
  tolerant of a few unloadable types: `NUnitTolerantAssemblyBuilder` uses the
  types that did load instead of marking the whole assembly `NotRunnable`.
  Testhost discovery uses the same builder so assembly-level attributes and
  sort order match the host.
  That builder also sets NUnit `TestContext.WorkDirectory` (the generation
  shadow). Accessing `WorkDirectory` before this runs throws.
  On net48 there is no load context: if the host already loaded a product
  assembly with the same identity (name, version, public key token), in-host
  tests bind to that copy, not the generation snapshot. Restart the host after
  deploying a matching add-in, or run on net8+ where the generation uses an
  `AssemblyLoadContext`.
- Running a test starts or reuses the selected host and sends only the neutral
  `testing/hello`, `testing/run`, and `testing/cancel` contracts. Discovery UID,
  wire `TestId` for host `<test>` is NUnit `ITest.FullName`. The TestNode uid
  is the same string except for `[TestCase(TestName=)]` / `SetName` leaves,
  which use `Class.Method("DisplayName")` so MTP IDEs do not index
  `Class.Unit_X` as a second method beside the `Named_basis_length_is_one`
  group. Discovery TestNodes carry class/method identity on
  `TestMethodIdentifierProperty`: `TypeName` is the C# source type without
  namespace (no fixture constructor arguments, no closed generic args, no
  ECMA-335 backtick arity). Visual Studio binds that string to the syntax
  tree: both ``GenericClosedTests`1`` and `GenericClosedTests<Int32>` yield
  "No source available". PDB lookup still uses metadata names internally
  (`GenericClosedTests`1`, nested `+`). Closed generic args stay on the
  TestNode uid / `ITest.FullName`. Putting display types in `TypeName` also
  makes IDEs tokenize `.` inside constructor arguments as a hierarchy
  break. `TestFileLocationProperty` (PDB file/line) is the line-accurate
  fallback when the identifier bind is enough to open the file.
  Filter: UID / `--filter-uid` / Test Explorer send the TestNode uid.
  Testhost may emit a NotRunnable stub `Class.Method` when
  `[TestFixtureSource]` / `[TestCaseSource]` cannot expand (Revit types at
  load). That UID stays the TestNode identity. The host filter also matches
  in-host `Class("args").Method` and `TestName` / `SetName` children
  (`ITest.FullName`); results fold back onto the requested UID and onto
  discovered leaf UIDs. Unfiltered runs (no `--filter` / `--filter-uid`)
  remap host `FullName` onto those discovered UIDs so `TestName` /
  `SetName` TestNodes receive results. Names-only `--filter` keeps per-leaf
  host identities. IDs that already include `(args)` stay exact
  `<test>`. Result TestNodes reuse the discovered `TestMethodIdentifier`
  (C# method name). `--filter` / `Name=` stay `Names` → `<name re="1">`
  (NUnit name regex). `--filter-uid` is the json TestNode uid: ordinary
  leaves are `ITest.FullName`; `TestName` / `SetName` leaves are
  `Class.Method("DisplayName")`. PowerShell: quote uids that contain `"`
  (`--filter-uid 'Ns.Class.Method("Unit_X")'`).
  A requested UID the host still does not report is published as Failed
  (same identity) instead of dropped. MTP `TestFrameworkCapabilities`
  stay empty (no VSTest-bridge extras).
- NUnit does not own the protocol. IDE-facing types are platform `TestNode`;
  host-facing types are `testing/*`. NUnit owns discovery tree, identity,
  filter XML, skip/explicit, and parameterized-case naming.

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

## Sample and execution

The maintained samples are:

- `samples/DevTools.NUnit.SampleTests` for Revit;
- `samples/DevTools.NUnit.Civil3D.SampleTests` for Civil 3D.

Those samples still use NUnit attributes because NUnit is the default engine.
`samples/ricaun.NUnit.SampleTests` is a comparison sample: it links the same
`HostSmokeTests` and runs them through `ricaun.RevitTest.TestAdapter` (VSTest).
It is not the product contract. Do not use it as the verify path, and do not
try to make it MTP.

Visual Studio Test Explorer **Debug** attaches the testhost; Runner then
EnvDTE-attaches that Visual Studio instance to the Autodesk host
([0025](../decisions/0025-runner-owned-visual-studio-host-attach.md)).
Rider and C# Dev Kit attach the host PID and **Run** (attach does not block
test execute). VS Code/forks and PyCharm attach Python via `debugpy` port
5678 (`.vscode/launch.json`, `.run/Attach.run.xml`).

Run the generated test executable or use the Microsoft.Testing.Platform
`dotnet test`/IDE surface provided by the sample-folder `global.json`. Discovery remains
host-free; host launch occurs only after an execution request. The adapter
copies `DevTools.NUnit.MTP.dll` next to the test exe. Consumers reference
NUnit; they do not add `DevTools.NUnit.MTP` as a ProjectReference.
Packaged modern TFMs copy a private `build/runtime` folder: exact
`net{version}-windows7.0` when it exists, otherwise the nearest lower
shipped folder (`net9` → `net8.0-windows7.0`). A consumer TFM with no
match fails the test project build instead of copying nothing.

Use an Autodesk configuration (`Debug.Autodesk.2024`, `Release.Autodesk.2024`,
…). Plain `Debug` / `Release` do not set `RevitVersion` / `TargetFramework`;
the sample does not build, and Test Explorer then shows a source/method tree
that is not MTP `ExploreTests`.

Canonical `samples/DevTools.NUnit.SampleTests` discovery is the test exe
`--list-tests json` leaf count. Measured **70** for both
`Debug.Autodesk.2024` and `Release.Autodesk.2024` (same UIDs):

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

Test Explorer counts are not that leaf count:

- `[TestCase(TestName = "Unit_X")]` is visible to Visual Studio Real-Time
  Discovery (Roslyn). RTD adds three extra Not Run children under
  `Named_basis_length_is_one` that never match MTP uids
  (`Class.Method("Unit_X")`). `TestCaseTests` becomes 8 + 3 = 11. `.SetName`
  cases do not get those extras because they are not in the attribute list.
  NUnit documents the same gap ([nunit3-vs-adapter#1256](https://github.com/nunit/nunit3-vs-adapter/issues/1256),
  [#489](https://github.com/nunit/nunit3-vs-adapter/issues/489)). Adapter
  code cannot dedupe RTD nodes. Turn off **Tools → Options → Test →
  Discover tests in real time from C# and Visual Basic .NET source files**,
  then refresh. Deleting `.vs/**/TestStore` only helps leftover hashes, not
  a live RTD pass.
- A ~32-node tree (methods, plus `GenericRevitTypeTests`, plus `TestName`
  leaves as extra methods) is grouping/source discovery, not the 70-leaf
  CLI list. Run from that tree does not send expanded FullName UIDs.

## Packaging

Two artifacts. The installer workflow does not publish the NuGet; the adapter
workflow does not pack the host bundle.

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
