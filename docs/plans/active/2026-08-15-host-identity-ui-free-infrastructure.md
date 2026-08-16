# Execution Plan: Host identity and UI-free infrastructure

Date: 2026-08-15

## Status

In progress — ADR [0018](../../decisions/0018-host-identity-and-out-of-process-infrastructure.md)
is **Accepted**. A, J, B, C+D, E, D2, H, I (`1963c030`) are on `develop`.
Hosting slim (`59a14a4b`) + Opus 5 graph follow-up are on `develop` (unpushed).
**G** (host-boundaries) lands with this follow-up. **K** (`Utilities/Interop`)
is **rejected** — launch P/Invoke stays in Hosting. Optional **F** remains.
Revit 2025 live launch passed; Acad-family skipped. Live pane after H not yet run.

## Outcome

Identity lives in `DevTools.Hosting`. Per-host **launch** (path / args /
dialog) lives in `DevTools.Hosting.Revit` / `Hosting.Acad` and is wired only
via `AddRevitLaunch()` / `AddAutocadFamilyLaunch()`. Shared-assembly names
are a `HostApiAssemblySet` in Utilities; add-ins call
`HostSharedAssemblies.Use(set)` in `Application.OnStartup` (not fake DI).
Daemon and Runner compose; they do not `switch (HostApp)` or `new HostLaunchService()`.
Logging is a headless ZLogger provider (no WPF/Scintilla). The pane lives in
Presentation. Telemetry does not pull Settings/UI. NUnit.Host keeps ZLogger
via headless Logging. Runner does not pull FileMetadata or WPF to know
`HostApp` or find `Revit.exe`.

## Context

- Decision: `docs/decisions/0018-host-identity-and-out-of-process-infrastructure.md`
- Boundaries: `docs/agents/host-boundaries.md` (updated in G)
- Logging architecture: `docs/architecture/Logging/README.md` (ZLogger is the
  in-process provider; MEL `ILogger<T>` is the code contract)
- Build: `.agents/skills/build/SKILL.md`
- Independent reviews: Opus 5 (layering) then GPT 5.6 sol (file-level P1s);
  Opus 5 C+D DI review (2026-08-16) — keep 4.2 shape, split shared-assembly
  out of C+D, required argument builder, opaque Options, `Add*Launch` /
  `Add*InProcess`

## Scope

In scope (Accept sequence):

- A, B, **C+D (one PR, launch only)**, E, **D2 (shared-assembly)**, H, I, J, G

Optional after graph is stable:

- F (`Agents.*` → `Mcp.Revit` / `Mcp.Acad`) — rename only, not an Accept
  condition

Out of scope:

- `IHostPlugin` / `Hosting.Abstractions` / `Logging.Abstractions` / `Logging.Ui`
- `AddMatchingInterfacesAsTransient` / keyed `HostApp` DI
- Putting Revit/Acad launch policy in `DevTools.Daemon` (composition only:
  `AddRevitLaunch(...)` / `AddAutocadFamilyLaunch()`)
- Folding `IHostSharedAssemblyPolicy` into C+D (static ALC hooks have no DI)
- `Hosting.Revit` → `FileMetadata.Revit` (would give Runner OpenMcdf)
- `Hosting.Civil3d` in **C+D** (later extract via `AddCivil3dLaunch()` is fine;
  do not do it in this PR)
- Splitting Execution off `DevTools.UI` (`TreeNodeBase`)
- Unifying Mcp.Server’s direct ZLogger package onto `DevTools.Logging`
- Sharing `ProductIdMap` between `AcadPathResolver` and `AcadHostAppInfo`

## Approach

PRs in this order. **E before H is a compile constraint.**

```text
A identity
B FileHostApplication → HostApp
C+D generic launch + three launch contracts + Add*Launch; Daemon/Runner wire only
E strip WPF/Shell from Utilities
D2 shared-assembly policy + Add*InProcess (after E)
H headless Logging / pane to Presentation
I Telemetry graph
J Settings theme enum (numeric JSON)
F optional rename
G host-boundaries.md (+ one ARCHITECTURE line)
```

NUnit.Host **keeps** `DevTools.Logging` from A onward. It is UI-free only
after E+H. Until then, say so in the PR description — do not claim the
full P3 graph at A.

ZLogger performance: Host code stays `ILogger<T>`. Providers come from
headless `AddLoggingProvider` (hosts already call it before
`AddNUnitHostServices`). Do not add a second ZLogger PackageReference on
NUnit.Host.

## Risks And Recovery

- **CS0121:** delete both extra `IsAcadFamily` extensions in **A**, keep
  `AcadPathResolver.ProductIdMap`.
- **HostApp usings:** ~30 files import `DevTools.Logging` for identity *and*
  real logging types. Do not global-replace the namespace.
- **Compatible Revit year:** file year is a **minimum**, not an exact
  install. `RevitFileAwareHostLaunchService` (in **Hosting.Revit**) calls
  `FindCompatibleVersion`; Daemon only passes `TryReadRevitVersion`.
- **Acad argv:** C+D uses the provided shortcuts only (Civil Metric +
  Plant `/product PLNT3D`). No `/nologo`. Wrong `/product` looks like
  success — assert the pipe name.
- **Civil profile:** default `<<C3D_Metric>>`. Imperial is a later
  one-liner, not C+D.
- **Do not** add `source/DevTools.Daemon/Hosting/Revit*.cs`.
- **Theme JSON:** `FileConfig` persists `"theme": 0`, not `"Light"`.
- **Utilities WPF ride:** `HostUiHelper` / `Window` extensions compile today
  because Logging has `UseWPF`. H without E breaks Utilities.
- **Dialog resolver internals:** C+D must move the Win32 the resolver
  actually calls; do not make Utilities internals public.
- **Launch wait:** do not reintroduce a private `WaitOutcome` / deadline loop
  in `LaunchHostTool` or Runner `HostSession`. Ready probe stays at the
  caller; clocks stay in `HostLaunchWait`.
- **DI wiring:** do not `new HostLaunchService()` after C+D. Daemon and Runner
  call `Add*Launch`. Missing argument builder throws (never `?? []`).
  Do not put `"unsigned add-in"`, `"unsigned executable file"`, `#32770`,
  or `RevitAPI` in generic Hosting. No merged Autodesk dialog bag.
- **Shared-assembly:** C+D does not split `HostSharedAssemblies`. D2 uses
  ambient `Use(policy)` because ALC hooks are static.
- Recovery: each PR is independently revertable. Do not stack unreviewed
  graph PRs. If a host is running, `scripts/kill-host.ps1` before deploy.

## Progress

- [x] ADR 0018 drafted, independently reviewed, NUnit.Host/ZLogger corrected
- [x] Launch wait unified in `Utilities/Hosting` (`HostLaunchWait`, pytest-style
      dialog lifetime) — precursor, still moves in C+D
- [x] Opus 5 C+D review applied: launch-only C+D, D2 shared-assembly, required
      args builder, opaque Options, `Add*Launch` / `Add*InProcess`
- [x] ADR 0018 Accepted (2026-08-16) — implement A+J in parallel; B/C+D after A
- [x] PR A — identity (`f5576c0f` on `develop`; Host.Tests Trace flake is pre-existing, see `known-test-gaps.md`)
- [x] PR B — file enum (`0ed92680` on `develop`)
- [x] PR C+D — Hosting.Revit / Hosting.Acad launch + Daemon/Runner wire (`065c3f4a` on `develop`; Revit 2025 live pass; Acad-family skipped)
- [x] PR E — Utilities UI-free (`2ac56268` on `develop`)
- [x] PR D2 — shared-assembly policy + Add*InProcess (`1e7bf9d2` on `develop`)
- [x] PR H — Logging headless (`085921f1` on `develop`; live pane / nunit/run not run)
- [x] PR I — Telemetry (`1963c030` on `develop`)
- [x] PR J — Settings theme (`4a445bb3`, `f03ee5b3` on `develop`)
- [x] Hosting slim — Spec rename, fold launch types, policy in add-ins (`59a14a4b`)
- [x] Opus 5 graph follow-up — dead Win32/Configure, `HostApiAssemblySet`,
      `Use` at `OnStartup`, prefixes in Utilities, Composition folder,
      `HostAppParsing`, `AddAutocadFamilyLaunch` calls Core (`5ac4e8f3`)
- [ ] PR F — Agents rename (optional)
- [x] PR G — host-boundaries.md (+ ARCHITECTURE line + 0018 amendment)
- [x] PR K — **rejected** as `Utilities/Interop` (would create Hosting → Utilities)

## Decisions

- 2026-08-15: NUnit.Host not wiring ZLogger is a **gap**. Keep headless
  Logging. NUnit.Core stays MEL-only. Runner drops Logging.
- 2026-08-15: Module boundaries outrank NativeAOT.
- 2026-08-15: C+D merge. App-specific **launch** lives in `DevTools.Hosting.Revit`
  / `Hosting.Acad`. Daemon is composition only — no `Revit*` types in its tree.
  Shared-assembly is D2, not this PR.
- 2026-08-15: `Hosting.Revit` does not reference FileMetadata. File-aware
  ctor takes a func; Daemon wires OLE.
- 2026-08-15: E before H; NUnit.Host UI-free only after E+H.
- 2026-08-15: J preserves theme **ordinals**, not just names.
- 2026-08-15: F is optional after the graph, not Accept-blocking.
- 2026-08-16: Launch wait is one Hosting primitive (`HostLaunchWait.UntilAsync`
  → `HostReadyStatus`). Dialog resolver has no self-timeout (pytest); caller
  timeout is the safety valve. MCP/NUnit inject a ready probe — they do not
  own a wait loop.
- 2026-08-16: C+D launch contracts are three (`IHostPathResolver`,
  `IHostArgumentBuilder`, `IHostStartupDialogSpec`). Wiring is
  Speckle-shaped `AddHostLaunchCore()` / `AddRevitLaunch()` /
  `AddAutocadFamilyLaunch()` — not `IHostPlugin`, not keyed DI, not
  `new HostLaunchService()`. `Supports(HostApp)` selects in multi-host
  containers. Argument builder is required (no `?? []`). `HostLaunchRequest`
  uses an opaque Options bag, not `LanguageCode`. Acad family stays one
  project. Add-ins do not call launch extensions.
- 2026-08-16: Dialog catalogs are **closed and per-host**. Revit title:
  `unsigned add-in`. Acad family title: `unsigned executable file`. Preferred
  `always load` on each spec (duplicated). Blocked only `do not load` /
  `load once` (no `cancel`/`no`). Drop `questionable add-in`. Generic engine
  has no default lists — no merged Autodesk bag, no union-test. Match is
  Contains against **that spec only**.
- 2026-08-16: Shared-assembly is **D2 after E**. `IHostSharedAssemblyPolicy`
  is a singleton without `Supports()`. Consumption is ambient
  `HostSharedAssemblies.Use(policy)` because ALC hooks are static.
  `Configure(directory)` is not a fallback until it has a caller. MahApps
  prefixes: one owner (Execution). `Autodesk.` duplicated per host on purpose.
- 2026-08-16: Acad-family argv is the **provided shortcuts**, not a full
  Autodesk switch catalog. Civil 3D 2026: `/ld AecBase.dbx /p <<C3D_Metric>>
  /product C3D /language en-US`. Plant 3D 2027: `/product PLNT3D /language
  en-US`. **No** `/nologo` / `/nosplash`. C+D stays one `Hosting.Acad`;
  `Hosting.Civil3d` is a later extract (`IHostArgumentBuilder` seam) — not
  this PR. Other family hosts: `/product` + `en-US` only; do not invent
  Arch/MEP `/p`. Imperial Civil later. Public `languageCode` is .NET
  culture (`en-US`); builders map (Revit → `ENU` on argv only). DTO echoes
  culture, not `ENU`. Live: Civil3D 2026, Plant3D 2027, one Revit year.
- 2026-08-16: Rename `IHostStartupDialogStrategy` → **`IHostStartupDialogSpec`**.
  It is a closed per-host catalog (`CreateOptions()`), not routing and not
  the Win32 engine (`StartupDialogResolver`).
- 2026-08-16: **In-process vs out-of-process split is by who loads the
  assembly**, not by forcing Hosting to net10-only. Types that load **into**
  Revit/Acad register in `RevitDevTool` / `AcadDevTool` (`AddRevitInProcess`
  / `AddAutocadInProcess`). No Daemon/Runner workaround for in-process
  policy. Out-of-process launch stays `Add*Launch` (Daemon/Runner only).
  Hosting keeps multi-TFM while add-ins reference it. FileMetadata parsers
  stay **HostApp-free** (OLE/DWG do not take `HostApp`; family mapping stays
  at `FromExtension` / `FileInfoResult`). Do **not** merge FileMetadata into
  Hosting in this sequence — Runner must not take OpenMcdf. Revisit merge
  only if that constraint still holds.
- 2026-08-16: Assembly load has three jobs today — do not add a fourth
  scattered path. (1) Add-in deploy folder, once:
  `Utilities/AssemblyLoader.cs`. (2) Dynamic/command ALC:
  `Utilities/AssemblyLoading/*` + `IHostSharedAssemblyPolicy` (per-host
  names, no Revit↔Acad fallback). (3) NUnit generation loaders. Follow-up
  after I: map these to fewer entry points; do not redesign NUnit in that
  first cut.
- 2026-08-16: Native P/Invoke folds into `DevTools.Utilities/Interop/`
  (files by scope: stdio, dialog, window). Win32 only — **no** WPF types.
  `DevTools.UI/Win32Utils` may wrap Interop for owner/title-bar. Follow-up
  **K** after I. Do not leave duplicates in `HostLaunchService.StdioInheritance`
  / `Hosting/DialogNative` / `Utilities/Win32Utils`.
- 2026-08-16: **No `DevTools.Logging.Monitor`.** Headless Logging + pane in
  Presentation (H) already matches “every module logs without WPF”. Keep ADR
  reject of `Logging.Ui`.
- 2026-08-17: Assembly policy lives in **Utilities** (`HostApiAssemblySet`
  + `HostSharedAssemblies`). Host-specific names are `RevitHostApiAssemblies`
  / `AcadHostApiAssemblies` in add-in `Composition/`. `Use(set)` runs in
  `Application.OnStartup` next to `AssemblyLoader.Initialize()` — not
  `Add*InProcess`. No mixed Revit+Acad fallback. Generic Hosting does not
  own this. UI-package prefixes (`MahApps.` / `ControlzEx.` /
  `CommunityToolkit.`) live in Utilities. Dialog result is
  `StartupDialogResult` (clicked titles, remaining titles, `Resolved` /
  `ClickCount`) — no `DialogEvent` / `DialogResolution` / always-false
  `TimedOut`. Resolver session is nested in `StartupDialogResolver`. Launch
  wait stays one loop (`HostLaunchWait` + `HostReadyStatus` in the same
  file). `SingleFor` is list-pattern `[]` / `[one]` / `_`. `FromExtension` /
  `IsAcadFamily` co-located with `HostApp` (enum cannot own methods).
- 2026-08-17: Keep three FileMetadata projects. Do not merge into Hosting.
  Keep `FileInfoResult` as wire vocabulary; source files use FileMetadata.
- 2026-08-17: **K rejected.** Do not fold launch P/Invoke into
  `Utilities/Interop` (Hosting would depend on Utilities). Dead
  `Utilities/Win32Utils` deleted. Remaining native code stays in Hosting.
  `DevTools.UI/Win32Utils` stays WPF-typed. Directory-scan `Configure` and
  unused `EnsureRegistered` deleted — three load jobs, two live entry points.

## Validation

Per-PR compile/test is in the PR cuts below. Repository-required: build
skill; focused `scripts/test-dotnet.ps1`; live pane / `launch_host` /
`nunit/run` only where a PR touches that path.

Host compile-only (when a PR touches Revit/Acad csproj):

```powershell
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Net48 gate: C+D Hosting projects via `-c Debug` (multi-tfm includes net48).
E, H, D2 host add-ins: `-c Debug.Autodesk.2022`.

## Result

| Commit | Evidence |
|--------|----------|
| A–I, J | On `develop` through `1963c030`. Revit 2025 `launch_host` live pass. Acad-family live skipped. Live pane / `nunit/run` after H not run. |
| Hosting slim `59a14a4b` | Spec rename; launch types folded; policy moved to add-ins. |
| Opus 5 graph follow-up `5ac4e8f3` | Compile: Utilities/Hosting/Hosting.Revit/Hosting.Acad/Execution/Mcp.Server Debug; Revit + Acad `Debug.Autodesk.2025` compile-only. Tests: Utilities 16, Hosting 48, Hosting.Revit 11, Hosting.Acad 13, Logging 7. NUnit.Host 62 pass / 1 fail (`Run_reports_pass_and_fail_results` Trace — `known-test-gaps.md`). Mcp.Tests 194 pass / 3 known gaps (UI thread tracker, live host, python parser sample). |

Remaining on this plan: optional **F**. Move to `docs/plans/completed/` after F or when abandoning F.

---

## PR cuts

Copy `RevitDevTool.slnx` Hosting/test entries from an existing Shared /
Tests project (full `BuildType` map, `Build Project=false` on tests).

Do not edit `Directory.Packages.props` unless a package actually moves
(Scintilla in H, Shell in E).

### PR A — `DevTools.Hosting` identity

First landable PR. Does **not** finish UI-free for NUnit.Host.

Create:

- `source/DevTools.Hosting/DevTools.Hosting.csproj` —
  `net48;net8.0-windows;net10.0-windows`, no `UseWPF`, no Logging
- `source/DevTools.Hosting/HostApp.cs`
- `source/DevTools.Hosting/IHostAppInfo.cs`
- `source/DevTools.Hosting/HostAppExtensions.cs` (`FromExtension`,
  `IsAcadFamily`)
- `tests/DevTools.Hosting.Tests/` + `HostAppExtensionsTests` +
  `HostingAssemblyBoundaryTests`

Delete:

- `source/DevTools.Logging/IHostAppInfo.cs` (enum + interface)

Edit (identity `using` only — leave real logging usings):

- Utilities `Hosting/*.cs`, `Hosting/Resolver/AcadPathResolver.cs` — delete
  local `IsAcadFamily`
- `DevTools.Mcp.Server/Utils/HostAppExtensions.cs` — delete
  `IsAcadFamily` / `FromExtension` / `ExtensionMap`; keep `FromPipeName` /
  `ParseHostApp`
- Runner: `Services/HostSession.cs`, `Commands/RunCommand.cs`,
  `Commands/DiscoverCommand.cs` — Hosting instead of Logging
- `DevTools.NUnit.Host/NUnitRequestHandler.cs` + tests
- Execution pipe/handlers that take `HostApp` / `IHostAppInfo`
- Revit/Acad `HostAdapters/*HostAppInfo.cs`, compiled-script bridges,
  `HostBackgroundController`
- Telemetry `SentryTelemetryService.cs`, `TelemetryServiceRegistration.cs`
- Mcp.Server `LaunchHostTool.cs`, `Contracts/InstanceContracts.cs`
- Daemon `Contracts/ControlPipeResponses.cs`
- Logging `Targets/FileLogProcessor.cs` — `IHostAppInfo` from Hosting
- Presentation `GeneralSettingsViewModel.cs` if it uses `IHostAppInfo`

csproj:

- Add Hosting to Logging (FileLogProcessor), Utilities, NUnit.Host,
  Execution, Telemetry, Presentation, hosts, Daemon, Mcp.Adapter as needed
- **Runner: replace Logging with Hosting**
- **Mcp.Server: replace Logging with Hosting; keep ZLogger package**
- **NUnit.Host: add Hosting, keep Logging**
- slnx: Hosting + Hosting.Tests

New tests:

- Extension → family (` .rvt` Revit, `.dwg` AutoCad, unknown null,
  case-insensitive). `.dwg` never Civil3D
- `IsAcadFamily`
- Hosting forbids UI, Logging, FileMetadata, PresentationFramework
- NUnit.Host still references Logging; forbids Presentation/UI/Scintilla
  **directly** (transitive WPF via Utilities/Logging is documented until E+H)
- Runner forbids Logging

Compile:

```powershell
dotnet build source/DevTools.Hosting/DevTools.Hosting.csproj -c Debug
dotnet build source/DevTools.Logging/DevTools.Logging.csproj -c Debug
dotnet build source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Debug
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug
```

Plus Revit/Acad 2025 compile-only.

Tests:

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.Hosting.Tests/DevTools.Hosting.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runner.Tests/DevTools.NUnit.Runner.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
```

Must not include: launch move, FileHostApplication delete, Logging WPF split,
telemetry, Agents rename.

Rollback: revert the new project + usings/refs as one commit.

### PR B — `FileHostApplication` → `HostApp`

Edit:

- `source/DevTools.FileMetadata.Core/ReadingContracts.cs`
- `source/DevTools.FileMetadata.Revit/RevitFileInfoReader.cs`
- `source/DevTools.FileMetadata.Acad/AcadFileMetadataReader.cs`
- FileMetadata.Core → Hosting
- `tests/DevTools.Mcp.Tests/ContractTests.cs`

New tests: golden JSON `"hostApp":"Revit"` / `"AutoCad"`; FileMetadata.Core
forbids UI/Logging/Presentation.

```powershell
dotnet build source/DevTools.FileMetadata.Core/DevTools.FileMetadata.Core.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
```

Must not include: launch policy, reader `SupportedExtensions` deletion.

### PR C+D — generic launch + host launch DI

One PR. **Launch only.** **Do not** put Revit year policy in Daemon. **Do
not** split `HostSharedAssemblies` or add `IHostSharedAssemblyPolicy` here.
Speckle shape: contracts in `DevTools.Hosting`, implementations in
`Hosting.Revit` / `Hosting.Acad`, composition roots call `Add*Launch`.

Create / move into `DevTools.Hosting` (already exists from A):

- Contracts: `IHostPathResolver`, `IHostArgumentBuilder`,
  `IHostStartupDialogSpec`, `HostLaunchRequest` (`HostApp`, `Version`,
  `FilePath?`, opaque `Options` — **no** `LanguageCode`)
- Engine: `IHostLaunchService`, `HostLaunchService` (`IEnumerable<T>` +
  `Supports(HostApp)` — **no** `switch`, **no** `?? []` for args),
  `HostLaunchWait`, `HostLaunchCoordinator`, `StartupDialogResolverHandle`,
  dialog Win32 poller **without** product keywords or window/button class
  names, `AddHostLaunchCore()`
- Move from `Utilities/Hosting/` (delete copies)

Create `DevTools.Hosting.Revit`:

- `RevitPathResolver`, `RevitArgumentBuilder` (`Options["language"]` =
  culture `en-US` → argv `/language ENU`; default omit = `en-US`; **no**
  `/nosplash`),   `RevitStartupDialogSpec`
  (title `unsigned add-in`; preferred `always load`; blocked `do not load`,
  `load once`; window/button classes on this spec),
  `FindCompatibleVersion`, file-aware decorator (func, no FileMetadata
  ProjectReference), `AddRevitLaunch(readDocumentYear)`

Create `DevTools.Hosting.Acad`:

- `AcadPathResolver` + `ProductIdMap`, `AcadArgumentBuilder` (provided
  shortcuts: Civil `/ld`+`/p <<C3D_Metric>>`+`/product C3D`; Plant
  `PLNT3D`; **no** `/nologo`; other family `/product`+`en-US` only), `AcadStartupDialogSpec` (title `unsigned executable file`;
  same preferred/blocked pair on **this** spec, not a shared default),
  `AddAutocadFamilyLaunch()` — `Supports()` all Acad-family `HostApp`
  values. `Hosting.Civil3d` is a later extract, not this PR.

Composition roots (delete `new HostLaunchService()`). Add-ins **do not**
call these:

```csharp
// Daemon ServerHostBuilder
services.AddHostLaunchCore();
services.AddRevitLaunch(RevitFileMetadataReader.TryReadRevitVersion);
services.AddAutocadFamilyLaunch();

// NUnit Runner — ConsoleApp.ServiceProvider in Program.Main
services.AddHostLaunchCore();
services.AddRevitLaunch(readDocumentYear: null);
services.AddAutocadFamilyLaunch();
// NUnitRunnerCommands(HostSession) — MEDI allowed on Runner
```

No new file under `source/DevTools.Daemon/Hosting/`. `Mcp.Server` still
takes `IHostLaunchService`; no reference to Hosting.Revit / Acad.
`LaunchHostTool` = spawn + `UntilAsync` + session probe.
Runner `HostSession` = oldest-PID reuse then `UntilAsync` + pipe probe.
`StartupDialogResolverHandle` takes options from the spec.

New tests:

- `HostLaunchWait`: Ready / Exited / TimedOut / Cancelled
- `HostLaunchService` source contains no `switch (HostApp` / `IsAcadFamily`
- At most one `Supports(host)` per contract on the **shared `Add*` helper
  both roots call**; missing path **or** args ⇒ not-supported (never empty
  argv)
- Keep file-not-found and language: MCP/Options culture `en-US` → Revit argv
  `ENU`; Acad argv stays `en-US`; unmapped culture throws; MCP DTO echoes
  `en-US` not `ENU`; tool description is not “Revit ENU”
- Acad argv goldens: Civil3D = `/ld` + `/p <<C3D_Metric>>` + `/product C3D`
  + `/language en-US`; Plant3D = `/product PLNT3D` + `en-US` and **not**
  `/ld`; AutoCad = `/product ACAD` + `en-US`; **no** `/nologo` / `/nosplash`
- Explicit version skips metadata; `.rvt` 2025 + only 2026 installed → 2026
- Revit dialog spec: title only `unsigned add-in`; blocked only
  `do not load` / `load once`; no `questionable add-in`
- Acad-family dialog spec: title only `unsigned executable file`; same
  blocked pair; does not contain `unsigned add-in`
- Generic Hosting forbids `"unsigned add-in"`, `"unsigned executable file"`,
  `"questionable add-in"`, `#32770`, `RevitAPI`, `acmgd`
- Hosting.Revit forbids FileMetadata, OpenMcdf, UI, MahApps strings
- Runner forbids FileMetadata, Logging, Shell; no `new HostLaunchService()`;
  MEDI is allowed
- Mcp.Server forbids FileMetadata.Revit and Hosting.Revit
- No `Revit*` type in the Daemon assembly besides composition

```powershell
dotnet build source/DevTools.Hosting/DevTools.Hosting.csproj -c Debug
dotnet build source/DevTools.Hosting.Revit/DevTools.Hosting.Revit.csproj -c Debug
dotnet build source/DevTools.Hosting.Acad/DevTools.Hosting.Acad.csproj -c Debug
dotnet build source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj -c Debug
dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Debug
dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.Hosting.Tests/DevTools.Hosting.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.Hosting.Revit.Tests/DevTools.Hosting.Revit.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.Hosting.Acad.Tests/DevTools.Hosting.Acad.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runner.Tests/DevTools.NUnit.Runner.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
```

Live **CLI automation** (this machine; C+D does not ship without it):

- `launch_host` Civil3D 2026 → process is C3D, pipe `DevTools_Civil3D_2026_*`
  (not `DevTools_AutoCad_*`). Profile `<<C3D_Metric>>`.
- `launch_host` Plant3D 2027 → pipe `DevTools_Plant3D_2027_*`.
- `launch_host` Revit (one of 2022–2027) → argv `/language ENU`, result
  `languageCode: "en-US"`, pipe `DevTools_Revit_{year}_*`.
- Runner `--host civil3d --host-version 2026 --host-launch` and
  `--host plant3d --host-version 2027 --host-launch` still `filePath: null`.

Arch/Mech/MEP/Elec/Map: `/product` + `en-US` only — do not invent `/p`.

Must not include: folder picker, Logging pane, telemetry, Agents rename,
`RevitVersionResolvingHostLaunchService` in Daemon, `IHostPlugin`, keyed DI,
`IHostSharedAssemblyPolicy`, add-in ALC changes.

### PR E — Utilities UI-free (before H)

Move:

- `AppUtils.SelectFolder` + `Microsoft-WindowsAPICodePack-Shell` →
  Presentation. Callers:
  `CommandViewModel`, `LogSettingsViewModel`, `StubBuilderViewModel`,
  `McpRegistryViewModel`
- `HostUiHelper` + `Win32Utils.SetHostAppOwner` → `DevTools.UI` (or
  Presentation). Update host/Execution/Daemon/Presentation usings.

Drop Utilities → Logging and Shell. Keep `AssemblyLoading` for NUnit.Host.

New tests: Utilities forbids PresentationFramework, DevTools.UI, Logging,
FileMetadata, Autodesk strings; NUnit.Host still compiles against
AssemblyLoading.

```powershell
dotnet build source/DevTools.Utilities/DevTools.Utilities.csproj -c Debug
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Live: each folder picker; pane still opens (Logging still has Scintilla).

Must not include: Scintilla move (that is H).

### PR D2 — shared-assembly policy (after E)

Not C+D. Callers of `HostSharedAssemblies.IsShared` are static ALC hooks
(`CommandLoadContext`, `HostAssemblyResolver`, `DirectoryAssemblyLoad`,
NUnit.Host loaders). They cannot take constructor DI.

Add in `DevTools.Hosting`: `IHostSharedAssemblyPolicy` (host-API simple
names + prefixes). Framework prefixes (`System.`, `mscorlib`) stay generic.

Add in `Hosting.Revit` / `Hosting.Acad`:

- `RevitSharedAssemblyPolicy` / `AcadSharedAssemblyPolicy`
- `AddRevitInProcess()` / `AddAutocadInProcess()` — plain singleton,
  **no** `Supports()`
- `HostSharedAssemblies.Use(policy)` once at add-in startup

Prefix ownership:

| Prefix | Owner |
|--------|--------|
| `RevitAPI`, `acmgd`, … | host policy |
| `Autodesk.` | **both** host policies (duplicated on purpose) |
| `MahApps.`, `ControlzEx.`, `CommunityToolkit.` | **one** owner: Execution (`HostPackagePrefixes`); NUnit.Host calls through it |

Do **not** redesign NUnit.Host loaders. Do **not** treat
`Configure(hostApiDirectory)` as a fallback until something calls it.
Daemon / Runner / Mcp.Server do **not** register in-process policy.

```csharp
// RevitDevTool / AcadDevTool only
services.AddRevitInProcess();   // or AddAutocadInProcess()
```

New tests: `IsShared("RevitAPI")` / `acmgd` still true after `Use(policy)`;
generic Hosting has no MahApps strings; Execution owns UI-package prefixes;
add-ins do not call `AddRevitLaunch()`.

```powershell
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
scripts/test-dotnet.ps1 -Project tests/DevTools.Hosting.Tests/DevTools.Hosting.Tests.csproj
```

Must not include: Logging pane, launch `Add*` changes, NUnit.Host loader
rewrite.

### PR H — headless Logging

Edit `DevTools.Logging.csproj`: drop `UseWPF`, `ZLogger.Scintilla`,
`Scintilla5.NET`.

Split `TraceListenerHelper`: `Trace.Listeners` stays; `PresentationTraceSources`
→ Presentation.

Move to Presentation: `IMonitorLogTarget`, `MonitorLogTarget`,
`AddMonitorLogging`. Presentation takes `ZLogger.Scintilla` and net48
`Scintilla5.NET`.

Split `AddLoggingProvider`: headless = config + notify + file + HTTP (no
`ScintillaOptions`). Hosts call Presentation opt-in for monitor + WPF trace.

`RevitHostingExtensions.AddLoggingServices` /
`AcadHostingExtensions` same: headless then monitor.

NUnit.Host: still references Logging; add a test that a headless
`IServiceCollection` + `AddLoggingProvider()` resolves `ILogger<NUnitHost>`
without Presentation.

Create `tests/DevTools.Logging.Tests` (no Logging test project today):
headless registration; Logging forbids PresentationFramework, UI, Scintilla.

```powershell
dotnet build source/DevTools.Logging/DevTools.Logging.csproj -c Debug
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2022 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Live **net48 pane** is mandatory. Live `nunit/run` must still log through
the host ZLogger pipeline.

Must not include: telemetry, Settings theme.

### PR I — Telemetry

`DevTools.Telemetry.csproj`: Hosting + Sentry only. Drop Settings, Logging,
Utilities.

`TelemetryServiceRegistration`: host/Daemon pass `enable`, `dsn`,
`IHostAppInfo`. Inline `InstallationId` path helper.

Both `HostBackgroundController` implementations currently delay
`LoadSettings()` — the factory must not require settings at first resolve
if today’s behavior is delayed enable.

Add tests in `tests/DevTools.Telemetry.Tests` (create if missing): NoOp when
disabled; Sentry when enabled; forbids Settings/UI/Logging/Utilities.

```powershell
dotnet build source/DevTools.Telemetry/DevTools.Telemetry.csproj -c Debug
```

Plus Revit/Acad 2025 compile-only.

### PR J — Settings drops UI

Create Settings-owned enum with **explicit ordinals** 0/1/2. Golden fixture
of existing `"theme": 0` JSON.

Map at Presentation / host SettingsService / HostBackgroundController.
Do **not** change `DaemonSettings` to a second enum if it can share the
Settings type; Daemon is WPF and may keep mapping to `DevTools.UI.Theme`.

Drop Settings → UI. MCP Catalog closure has no MahApps.

```powershell
dotnet build source/DevTools.Settings/DevTools.Settings.csproj -c Debug
dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
```

Live: load old settings file, switch Light/Dark/Auto.

### PR F — optional rename

`DevTools.Agents.Revit` → `DevTools.Mcp.Revit`, `Agents.Acad` →
`Mcp.Acad`. slnx, host ProjectReferences, hosting extension usings,
`.agents/skills/build/SKILL.md` project names. Assemblies stay host-bound
(`UseRevit`). Not an Accept condition.

### PR G — docs

Landed with the Opus 5 graph follow-up. Agent layer:
`docs/agents/host-boundaries.md` (identity / launch / FileMetadata /
assembly-load / Composition). One paragraph in `docs/ARCHITECTURE.md`.
0018 amendment 2026-08-17. 0002 host-neutrality not retracted.

### PR K — rejected (`Utilities/Interop`)

Do **not** move Hosting P/Invoke into `DevTools.Utilities/Interop/`. That
would create `Hosting → Utilities` and pull a leaf (12 identity consumers)
into a dependent. After deleting dead `Utilities/Win32Utils`, remaining
native code is `HostLaunchService.StdioInheritance` and `DialogNative`
(both internal to Hosting). WPF owner/title-bar stays `DevTools.UI/Win32Utils`.

Must not include: Logging.Monitor, FileMetadata merge, NUnit loader rewrite.
