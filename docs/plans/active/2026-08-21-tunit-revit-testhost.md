# Execution Plan: TUnit Provider Runtime In Revit

Date: 2026-08-21

## Status

Spike redirected to TUnit.Core catalog in-host; Revit 2023 live sample proven

## Assessment

Overall: **amber / spike-ready, not production-ready**.

- Architecture boundary: complete. TestRunner and `Testing.Transport` are
  unchanged. In-host TUnit no longer constructs `TestApplication` / `AddTUnit()`.
- Shared contract: `TestingDiscoveryHints`, `TestingDiscoveryOptions`, and
  optional `TestingDiscoveredTest.HasDataSource` / `Categories` are additive.
  NUnit discoverer uses interface default methods and is unchanged.
- Local discovery: testhost and host share `TUnitCatalog` over
  `Sources.TestEntries` + `GetFilterData`. Parameterless UID remains
  `{Class}.1.1.{Method}.1.1.0`. Data-source rows expand through TUnit.Core
  `GetDataRowsAsync` (`[Arguments]`, MethodDataSource, Matrix, ClassDataSource,
  class/property data, Repeat). Property injection reflects when TestEntry
  omits `injectableProperties`. In-host invoke honors `[Retry]`.
- Compile/test confidence: high for this redirection. Adapter 50/50,
  Abstractions 16/16, NUnit MTP 37/37, Transport 17/17. RevitDevTool 2025
  compile-only succeeded. `TUnitRuntime\` contains `TUnit.Core` only (no MTP).
- Live confidence: Revit 2023 proven (`dotnet test` sample, 15 pass / 2 skip /
  1 expected demo fail). Revit 2025 live remains pending.
- Known spike limits: TUnit.Engine session/assembly hooks, TestContext hook
  parameters, and pairwise/combinatorial engines beyond Matrix remain out of
  scope.

## Outcome

Run TUnit 1.65.38 tests inside Revit through the existing TestAdapter ->
TestRunner -> `testing/run` IPC -> `IHostContextExecutor` flow. Live proof
uses two years (2023/net48 and 2025/net8); packaging follows the full host
TFM matrix. NUnit and TestRunner host lifecycle remain unchanged.

## Scope

In scope:

- Framework-specific TUnit catalog discovery, generation policy, isolated Core
  runtime, and neutral result mapping.
- Existing TestRunner host locate/start/reuse behavior.
- `TUnitRuntime` payload isolation for every Revit host TFM (`TUnit.Core` only).
- Source-generated, non-data-driven smoke tests.
- Additive testing-kernel discovery fields compatible with TUnit catalog and NUnit.

Out of scope:

- AutoCAD and Civil 3D TUnit support (`HostName` remains Revit-only).
- Nested MTP testhost / `TestApplication` inside the Autodesk host.
- TUnit.Engine session hooks, data-source UID expansion, and full cancellation.

## Progress

- [x] Pin latest stable TUnit 1.65.38 and MTP 2.3.3.
- [x] Remove the rejected `ITestHostLauncher` / `testing/testhost/start` path.
- [x] Add the TUnit local discovery sibling and retain the generic outer adapter.
- [x] Add TUnit generation/ALC provider over the existing testing kernel.
- [x] Replace nested MTP `TestApplication` with TUnit.Core catalog + invoke.
- [x] Extend neutral discovery contracts for catalog hints without changing NUnit.
- [x] Run focused adapter and contract tests (Abstractions 16, Adapter 50, NUnit MTP 37, Transport 17).
- [x] Compile RevitDevTool 2025 (compile-only) and TUnit sample; `TUnitRuntime\` ships `TUnit.Core` only.
- [x] Prove host-free `--list-tests` on net48 after the catalog (18 tests).
- [x] Build RevitDevTool 2023; publish TestRunner.
- [x] Run live smoke through TestRunner and the existing Revit IPC path (2023).
- [ ] Repeat live smoke in Revit 2025 after the satellite fix.
- [x] Run live smoke in Revit 2023 and confirm side-by-side STJ identities.

## Validation gate

```powershell
dotnet build .\source\RevitDevTool\RevitDevTool.csproj -c "Release.Autodesk.2025"
dotnet build .\source\RevitDevTool\RevitDevTool.csproj -c "Release.Autodesk.2023"
dotnet publish .\source\DevTools.TestRunner\ -c Release
```

Live validation must target only the Revit year under test. Other years must be
locked out of the test harness.
