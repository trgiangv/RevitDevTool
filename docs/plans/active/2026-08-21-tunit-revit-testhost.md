# Execution Plan: TUnit Provider Runtime In Revit

Date: 2026-08-21

## Status

Spike implementation and compile gates complete; live smoke remains pending

## Assessment

Overall: **amber / spike-ready, not production-ready**.

- Architecture boundary: complete. TestRunner and `Testing.Transport` are
  unchanged; no TUnit host launcher, activation command, dispatcher, or custom
  Revit executor remains.
- Local discovery and packaging: complete for Revit 2023/net48 and Revit
  2025/net8. Both configurations produce the same parameterless TUnit UID and
  keep TUnit/MTP outside the add-in root.
- Compile/test confidence: high. Adapter tests pass 49/49, generic testing-host
  tests pass 33/33, both host configurations build, and TestRunner publishes.
- Live confidence: incomplete. The first Revit 2025 run reached the existing
  TestRunner/IPC/generation path and exposed satellite resource assemblies in
  the managed manifest. TUnit now follows NUnit's existing rule and classifies
  satellite resources as `Other`; the corrected retry was interrupted before
  a final test result. Revit 2023 has not run live on the corrected provider.
- Known spike limits: source-generated non-data tests only; data-source UID
  expansion and cooperative cancellation remain out of scope.

## Outcome

Run TUnit 1.65.38 tests inside Revit 2025 (`net8.0-windows`) and Revit 2023
(`net48`) through the existing TestAdapter -> TestRunner -> `testing/run` IPC ->
`IHostContextExecutor` flow. NUnit and TestRunner host lifecycle remain unchanged.

## Scope

In scope:

- Framework-specific TUnit discovery, generation policy, isolated runtime, and
  neutral result mapping.
- Existing TestRunner host locate/start/reuse behavior.
- `TUnitRuntime` payload isolation for Revit 2023 and 2025.
- Source-generated, non-data-driven smoke tests.

Out of scope:

- Revit versions other than 2023 and 2025.
- AutoCAD, Civil 3D, and `net10` TUnit support.
- Any TUnit-specific host launcher, activation IPC, dispatcher, or test executor.
- Data-source UID expansion and full cancellation semantics during this spike.

## Progress

- [x] Pin latest stable TUnit 1.65.38 and MTP 2.3.3.
- [x] Remove the rejected `ITestHostLauncher` / `testing/testhost/start` path.
- [x] Add the TUnit local discovery sibling and retain the generic outer adapter.
- [x] Add TUnit generation/ALC provider over the existing testing kernel.
- [x] Run TUnit/MTP inside the provider runtime and map MTP result nodes.
- [x] Prove host-free list discovery on net8 and net48.
- [x] Run focused adapter tests (49/49).
- [x] Build RevitDevTool with the agreed 2025 and 2023 commands.
- [x] Publish the unchanged TestRunner.
- [x] Reach the existing TestRunner/IPC generation path in a live Revit 2025
  attempt and fix satellite resource classification using the NUnit rule.
- [ ] Run live smoke through TestRunner and the existing Revit IPC path.
- [ ] Repeat live smoke in Revit 2025 after the satellite fix.
- [ ] Run live smoke in Revit 2023 and confirm side-by-side STJ identities.

## Validation gate

```powershell
dotnet build .\source\RevitDevTool\RevitDevTool.csproj -c "Release.Autodesk.2025"
dotnet build .\source\RevitDevTool\RevitDevTool.csproj -c "Release.Autodesk.2023"
dotnet publish .\source\DevTools.TestRunner\ -c Release
```

Live validation must target only the Revit year under test. Revit 2026 processes
must never be stopped.
