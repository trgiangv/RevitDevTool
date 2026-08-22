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

- Testhost discovery is host-free: `DevTools.TUnit.MTP` reads
  TUnit.Core `Sources.TestEntries` / `ITestEntrySource.GetFilterData`
  (TUnit's default source-generated catalog) and publishes opaque TUnit UIDs
  in Engine `TestIdentifierService` form. It does not start a nested MTP
  `TestApplication`. Autodesk API compile refs resolve from
  `$(TargetName).discovery-refs.txt` (compile-only NuGet paths) via
  `TestingPlatformBuilderHook` static initialization — same pattern as
  NUnit `ExploreTests`; API DLLs are not copied next to the exe.
- Neutral `TestingSelection` may carry optional `Hints` (class/method/category)
  so TUnit can pre-filter before materializing entries. NUnit ignores hints and
  keeps ExploreTests + filter XML.
- Testhost expands `IDataSourceAttribute` rows from TUnit.Core so the IDE tree
  lists the same UIDs Engine will execute (`[Arguments]`, `[MethodDataSource]`,
  `[MatrixDataSource]`, `[ClassDataSource]`, class constructor data, property
  injection, `[Repeat]`). Data-source and loop indexes are 1-based to match
  Engine `TestBuilder` (`++` before first use; empty sources use
  `NoDataSource` as index 1). `[Repeat(n)]` expands `n + 1` times
  (`for (i = 0; i < repeatCount + 1; i++)`). Rows that
  cannot expand host-free stay as Engine `_Deferred` placeholders. The host
  does not parse or rewrite those UIDs: `TestingSelection.TestIds` is passed
  into `TestNodeUidListFilter` as opaque strings.
- In-host execution is a library call to `TUnit.Engine` inside
  `TUnitRuntimeSession.Run` (one Engine session per `testing/run`). Engine
  owns `[Before]/[After]`, Retry, DependsOn, Skip/Explicit, and timeout.
  `maximum-parallel-tests=1` so work stays on the Revit API thread. Nested
  `TestApplication` / `AddTUnit()` is not used.
- `samples/DevTools.TUnit.SampleTests` is split one scope per file: host smoke,
  lifecycle, each data-source attribute, Engine capabilities (Repeat, DependsOn,
  hooks, timeout, property injection, Retry), fixture shapes (inheritance, nested,
  generic closed/Revit types, constructor arguments), Revit geometry, generic
  helper methods, and document-bound deferred discovery gaps.
- TestRunner locates, reuses, or starts Revit and sends `testing/run` with those
  UIDs over the existing IPC pipe.
- `MarshaledTestingRequestHandler` enters `IHostContextExecutor`. The in-host
  `tunit` provider loads the generation isolation context and calls Engine.
- `TUnitRuntime` ships `TUnit.Core`, `TUnit.Engine`, and
  `Microsoft.Testing.Platform` under `TUnitRuntime\`, not at the add-in root.
- Collectible load contexts apply on modern Revit TFMs. .NET Framework years
  use scoped isolation and exact manifest identity resolution, including
  side-by-side `System.Text.Json` identities.
- In-host Engine execution clears `SynchronizationContext` so `await Task.Delay`
  / `Task.Yield` on the API thread does not deadlock `GetResult()`.

See `samples/DevTools.TUnit.SampleTests` for the supported project shape.
