# TUnit Host Testing

RevitDevTool runs TUnit tests through the same TestRunner and neutral
`testing/run` IPC path used by NUnit. TUnit is a framework-specific provider;
it does not launch or activate hosts. Shared MTP contract (launch, reuse,
cancel, adapter): [host-testing.md](host-testing.md).

Last updated: 2026-09-08

## Supported matrix

TUnit uses the same host year → TFM mapping as the host add-in
(`docs/agents/build-matrix.md`). Year values used in live proof are
verification evidence, not an allow-list.

| Host | TFM | TUnit |
|------|-----|-------|
| Revit | `net48` / `net8.0-windows` / `net10.0-windows` | 1.66.27 |
| AutoCAD / Civil 3D | `net48` / `net8.0-windows` / `net10.0-windows` | 1.66.27 |

NUnit remains the default framework. Use `TestingFramework=tunit` to opt in.

TUnit **1.66.27** and `Microsoft.Testing.Platform` **2.4.0** are a pair at
restore. Generation pins assembly versions only: `TUnit.Core` `1.66.27.0`
and `Microsoft.Testing.Platform` `2.4.0.0`. Testhost output and the in-host
`TUnitRuntime\` copy must match those identities. Generation fails closed on a
different DLL instead of mixing copies. Bump TUnit and MTP together in a
release.

## Test project

Revit:

```xml
<PropertyGroup>
  <UseRevit>true</UseRevit>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="TUnit" Version="1.66.27" />
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.5" />
</ItemGroup>
```

Civil 3D (same pattern as NUnit Civil 3D samples):

```xml
<PropertyGroup>
  <UseAutoCad>true</UseAutoCad>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Civil3D</HostName>
  <HostVersion>$(AutoCadVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="TUnit" Version="1.66.27" />
  <PackageReference Include="RevitDevTool.TestAdapter" Version="0.0.5" />
</ItemGroup>
```

For plain AutoCAD, set `<HostName>AutoCad</HostName>` with the same
`UseAutoCad` / `HostVersion` properties.

## Runtime behavior

Testhost discovery is host-free. `DevTools.TUnit.MTP` compile-links
`TUnitCatalog` / `TUnitExpansion` / `TUnitTestIdentity` from
`DevTools.TUnit.Runtime` and reads TUnit.Core `Sources.TestEntries` /
`ITestEntrySource.GetFilterData`. It does not start a nested MTP
`TestApplication`. Autodesk API compile refs resolve from
`$(TargetName).discovery-refs.txt` via `TestingPlatformBuilderHook` static
initialization — same pattern as NUnit `ExploreTests`; API DLLs are not
copied next to the exe.

Adapter load: `TestingFramework=tunit` writes `devtools.frameworkId` into
`testconfig.json`. The testhost hook loads **only** `DevTools.TUnit.MTP.dll`.
`maximum-parallel-tests` is **not** a `testconfig.json` / `HostTestConfig`
key. Wiring: [`docs/architecture/Testing/README.md`](../architecture/Testing/README.md).

### Discovery expansion

`TUnitExpansion` materializes each `ITestEntrySource` row and builds the
cartesian product **class data × method data × property injection ×
repeat**. Nested loops are the Engine product; they are not a workaround.

| Axis | Source | Index in UID |
|------|--------|--------------|
| Class data | `metadata.ClassDataSources` | 1-based `ClassSourceIndex` / `ClassLoopIndex` |
| Method data | `metadata.DataSources` | 1-based `MethodSourceIndex` / `MethodLoopIndex` |
| Repeat | `[Repeat(n)]` | 0-based `RepeatIndex`; Engine runs `n + 1` times |
| Property injection | `PropertyDataSources` or reflection fallback | **not in UID** (Engine `TestIdentifierService` has no property dimension) |

`TUnitCombinationIndices` carries the UID axes. Catalog display reads
`RepeatIndex`, method args, and property name/value pairs. Members that
look unused in `TUnitCatalog` are marked
`[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]` — do not drop them.
Empty data sources use Engine `NoDataSource` as index 1.

Property injection still expands for **display names**. Multiple property
combinations can share one UID; that matches Engine, not a host bug.
TUnit 1.65 `TestEntryFactory` omits `PropertyDataSources`; expansion
reflects writable properties with `IDataSourceAttribute` until a TUnit
bump fills metadata.

Rows that cannot `Materialize` host-free become one `Deferred` placeholder
(`TUnitTestIdentity.DeferredSuffix` = `_Deferred`). Testhost
(`TestingDiscoveryOptions.Testhost`, `ForExecution=false`) publishes that
placeholder UID. Host-run (`ForExecution=true`) uses the expanded
`TUnitTestIdentity.From` UID when metadata exists. The host does not parse
those strings: `TestingSelection.TestIds` goes into `TestNodeUidListFilter`
as opaque UIDs.

`TestingDiscoveryHints` (class/method/category) pre-filter before
materialize. NUnit ignores hints.

### In-host Engine

`TUnitRuntimeSession.Run` is one `TUnit.Engine` library call per
`testing/run`. Engine owns `[Before]/[After]`, Retry, DependsOn,
Skip/Explicit, and timeout. Nested `TestApplication` / `AddTUnit()` is not
used.

`TUnitEngineCommandLine` implements MTP `ICommandLineOptions` and answers
`maximum-parallel-tests` with `["1"]` so work stays on the host API
thread. `TUnitEngineConfiguration` answers
`platformOptions:resultDirectory`, `currentWorkingDirectory`, and
`testHostWorkingDirectory`. Those keys are Engine/MTP, not DevTools
config.

In-host Engine execution clears `SynchronizationContext` so
`await Task.Delay` / `Task.Yield` on the API thread does not deadlock
`GetResult()`.

`TUnitRuntime` ships `TUnit.Core`, `TUnit.Engine`, and
`Microsoft.Testing.Platform` under `TUnitRuntime\`, not at the add-in
root. Collectible load contexts apply on modern host TFMs. .NET Framework
years use scoped isolation. Manifest identity stays exact except net48
`NetfxClosureBind` (newer candidate in the generation manifest or already
loaded by that session — not a TUnit facade name list).

TUnit.Core `Sources.TestEntries` is a process-wide dictionary keyed by
`Type`. A rebuild loads a new test assembly (net48 cannot unload the old
one). The module constructor **adds** sources; Engine would then execute
every historical copy of the same UID and concatenate their `Console`
output. Before each discover/run, Runtime parks other assemblies' sources
and keeps only the current generation live. Reverting an edit reuses the
previous generation hash and the already-loaded assembly — the module
constructor does not run again — so parked sources are restored. Discarding
them made testhost report `TUnit did not report a result for the selected
test` with no stack. Parked maps live on parent-bound Abstractions
(`TestingProcessHold`), not Runtime statics: net48 `LoadFile`s a distinct
Runtime copy from each generation shadow folder while TUnit.Core stays
identity-bound, and the session manager retires the previous generation
before a revert recreates it.

### Testhost MTP copy

`CopyMTPSibling` overwrites `DevTools.TUnit.MTP.dll` next to the
test exe on every build (`SkipUnchangedFiles=false`) from `build/runtime`.
TUnit.MTP compile-links catalog files from `TUnit.Runtime`; a timestamp-skip
copy is why a rebuild can still run yesterday’s discoverer. Changing
in-host Engine/Runtime also requires rebuilding/deploying the host add-in
(`TUnitRuntime\`). On net48, if the host already loaded a matching
assembly identity, restart the host or use net8+ ALC.

### Test output

TUnit.Engine captures `Console` into MTP `StandardOutputProperty`. It does
not capture `Trace` / `Debug`. `TestingRunTraceScope` buffers those per
case, merges them into `CaseResult.Output` (Test Explorer), and
write-throughs Console to process `Trace` (host pane). Same split as NUnit
([0017](../decisions/0017-nunit-host-test-output-routing.md)). Do not add a
TUnit `TraceListener` or dump `CaseResult.Output` through `ILogger`.

TestRunner locates, reuses, or starts the selected host and sends
`testing/run` with the discovered UIDs. `MarshaledTestRequestHandler`
enters `IHostContextExecutor`. The in-host `tunit` provider loads the
generation isolation context and calls Engine.

Samples:

- `samples/DevTools.TUnit.SampleTests` — Revit coverage (data sources,
  lifecycle, geometry, deferred discovery gaps).
- `samples/DevTools.TUnit.Civil3D.SampleTests` — Civil 3D host smoke.
