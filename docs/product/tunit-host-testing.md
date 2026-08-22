# TUnit Host Testing

RevitDevTool runs TUnit tests through the same TestRunner and neutral
`testing/run` IPC path used by NUnit. TUnit is a framework-specific provider;
it does not launch or activate Revit.

Last updated: 2026-08-22

## Supported matrix

TUnit uses the same Revit year → TFM mapping as the host add-in
(`docs/agents/build-matrix.md`). Year values used in live proof (2023/net48,
2025/net8) are verification evidence, not an allow-list.

| Revit TFM | TUnit |
|---|---|
| `net48` | 1.65.38 |
| `net8.0-windows` | 1.65.38 |
| `net10.0-windows` | 1.65.38 |

AutoCAD and Civil 3D stay on NUnit (`HostName` must be `Revit` for TUnit).
NUnit remains the default framework.

## Test project

```xml
<PropertyGroup>
  <UseRevit>true</UseRevit>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="TUnit" Version="1.65.38" />
  <PackageReference Include="RevitDevTool.TestAdapter" />
</ItemGroup>
```

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
`maximum-parallel-tests` with `["1"]` so work stays on the Revit API
thread. `TUnitEngineConfiguration` answers
`platformOptions:resultDirectory`, `currentWorkingDirectory`, and
`testHostWorkingDirectory`. Those keys are Engine/MTP, not DevTools
config.

In-host Engine execution clears `SynchronizationContext` so
`await Task.Delay` / `Task.Yield` on the API thread does not deadlock
`GetResult()`.

`TUnitRuntime` ships `TUnit.Core`, `TUnit.Engine`, and
`Microsoft.Testing.Platform` under `TUnitRuntime\`, not at the add-in
root. Collectible load contexts apply on modern Revit TFMs. .NET Framework
years use scoped isolation and exact manifest identity resolution,
including side-by-side `System.Text.Json` identities.

TUnit.Core `Sources.TestEntries` is a process-wide dictionary keyed by
`Type`. A rebuild loads a new test assembly (net48 cannot unload the old
one). The module constructor **adds** sources; Engine would then execute
every historical copy of the same UID and concatenate their `Console`
output. Before each discover/run, Runtime keeps only sources whose
`ClassType.Assembly` is the current generation assembly.

### Testhost MTP copy

`CopyDevToolsMTPSibling` overwrites `DevTools.TUnit.MTP.dll` next to the
test exe on every build (`SkipUnchangedFiles=false`) and prefers the
in-repo MTP output over a leftover TestAdapter `build/runtime` nupkg copy.
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

TestRunner locates, reuses, or starts Revit and sends `testing/run` with
the discovered UIDs. `MarshaledTestingRequestHandler` enters
`IHostContextExecutor`. The in-host `tunit` provider loads the generation
isolation context and calls Engine.

`samples/DevTools.TUnit.SampleTests` is split one scope per file: host
smoke, lifecycle, each data-source attribute, Engine capabilities (Repeat,
DependsOn, hooks, timeout, property injection, Retry), fixture shapes
(inheritance, nested, generic closed/Revit types, constructor arguments),
Revit geometry, generic helper methods, and document-bound deferred
discovery gaps.
