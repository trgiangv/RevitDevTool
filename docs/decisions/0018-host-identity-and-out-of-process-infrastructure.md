# 0018 Host Identity And UI-Free Infrastructure

Date: 2026-08-15

## Status

Accepted. A–J, G, and optional F are on `develop`. **K** (`Utilities/Interop`) is rejected.

Does not change [0002](0002-host-agnostic-platform.md) or
[0007](0007-revit-core-and-visualization-boundaries.md) except one clarifying
sentence in Follow-Up: in-host MCP tool projects may reference their host
Core. 0002’s “host-neutral” axis (no Revit/Acad API in shared libraries)
stays true. This ADR adds two more axes: **UI-free** and **identity-not-in-Logging**.

Implementation is a multi-PR sequence. This document is the review contract.

## Context

### Jobs that currently share names and project references

| Job | Question | Today |
|-----|----------|--------|
| **Process identity** | Which product is this PID / pipe / launch target? | `HostApp` + `IHostAppInfo` in **`DevTools.Logging`** (`UseWPF`, Scintilla) |
| **File family** | Which product family does this path belong to? | Duplicate `FileHostApplication { Revit, AutoCad }` in `FileMetadata.Core`, plus `HostAppExtensions.FromExtension` in `Mcp.Server`, plus `IFileReader.SupportedExtensions` |
| **Install + spawn** | Where is `Revit.exe` / `acad.exe`, how do we `CreateProcess` without leaking stdio? | `DevTools.Utilities/Hosting/*`, and Utilities **ProjectReferences `FileMetadata.Revit`** so NUnit Runner pulls OpenMcdf |
| **Headless log** | File / HTTP / MEL provider for Daemon, MCP, NUnit | Same `DevTools.Logging` assembly as the Scintilla pane **and** WPF `PresentationTraceSources` (`TraceListenerHelper`) |
| **Pane log** | Dockable monitor in Revit/Acad | `MonitorLogTarget` + `IMonitorLogTarget.HostElement : FrameworkElement` + `ZLogger.Scintilla` |
| **Telemetry** | Sentry + `ITelemetry` | `DevTools.Telemetry` → Logging **and** Settings → **`DevTools.UI` (MahApps)** |
| **MCP catalog** | Registry of tools | `Mcp.Catalog` → Settings → UI, so **Mcp.Server / Mcp.Adapter pull MahApps** even though they never call WPF |

Symptoms:

- `FileHostApplication` is a coarser copy of `HostApp`. A `.dwg` cannot distinguish
  Civil 3D from vanilla AutoCAD; a running pipe/`ProductId` can.
- `IsAcadFamily` exists in three places: `AcadPathResolver`,
  `Mcp.Server.Utils.HostAppExtensions`, and (after step A) Hosting itself.
  `ProductIdMap` on `AcadPathResolver` is **not** a fourth `IsAcadFamily`; it
  must survive (Decision 2).
- `DevTools.Utilities` is a junk drawer: assembly load, Win32, folder picker
  (Shell), path resolvers, `HostLaunchService`, and a Revit OLE parser edge.
- `DevTools.NUnit.Runner` references Logging **only** for `HostApp`, and
  Utilities only for `DevTools.Utilities.Hosting`. It always calls
  `Start(..., filePath: null)` yet still depends on `FileMetadata.Revit`.
- `DevTools.NUnit.Host` references Logging today for `IHostAppInfo`, and uses
  MEL `ILogger<T>` with a `NullLogger` fallback. It has **no** ZLogger
  PackageReference of its own. That is a **gap**, not unused identity: in-process
  test execution should sink through **headless** `DevTools.Logging` (ZLogger
  file/HTTP/notify). The WPF host already calls `AddLoggingProvider` before
  `AddNUnitHostServices`, so live Revit/Acad DI is ZLogger — Host must keep
  that path after Logging loses Scintilla, and must not drop Logging just
  because the csproj has no direct ZLogger package.
- `DevTools.Mcp.Server` references Logging **only** for `HostApp`. It already
  PackageReferences ZLogger directly (`McpLogFilters`).
- `DevTools.Mcp.Revit` / `Mcp.Acad` are in-host MCP tools (`IBuiltInMcpTool`)
  and cheatsheet resources, not an “agent runtime”. `Mcp.Revit` sets
  `UseRevit` and references `RevitDevTool.Core`.
- `FileLogProcessor` takes `IHostAppInfo` to name rolling files. Headless
  Logging **must** reference Hosting after the enum moves; it must not
  re-export `HostApp`.
- `Settings.GeneralConfig.Theme` is typed as `DevTools.UI.Theme.AppTheme`,
  so `ISettingsService` is not a headless settings contract. That is why
  MCP is not UI-free today.
- `LoggingExtensions.AddLoggingProvider(HostApplicationBuilder, Action<ScintillaOptions>?)`
  is the real host entry (not `AddDevToolsLogging`). Its signature is
  WPF/Scintilla-typed; step H splits it, it does not merely “stop adding
  the monitor”.

**Priority:** module responsibility (identity ≠ logging ≠ pane ≠ launch) is
the design driver. Native AOT / `PublishTrimmed` of Runner is **not**.
EnvDTE stays in `Runner/Debugging/` per
[0016](0016-nunit-native-runtime-and-mtp-first-integration.md) decision 11.

## Principles (intentional abstraction)

These rules are the closed core. New hosts and features expand **beside**
them.

### P1. Layers are ranked, not peer

```text
Identity     HostApp / IHostAppInfo / FromExtension     no UI, no sinks
Headless     Logging providers, ITelemetry              MEL + Sentry; no WPF
Product      Mcp.Core, NUnit.Core/Transport             wire + ILogger<T>
Compose      hosts, Daemon, Runner, Presentation, UI
```

Logging and Telemetry are **infrastructure**. MCP and NUnit are **products**.
Identity is below both. A product may log; it must not own the pane.

### P2. Open/closed for a new Host App

| May change | Must not change |
|------------|-----------------|
| Add `HostApp.Tekla` (one enum member) | MCP JSON-RPC methods, `nunit/run` framing, MEL usage, `ITelemetry` |
| Add `IHostAppInfo` in the host project | `if (host == Tekla)` inside NUnit.Core / Mcp.Core |
| Add path resolver / MCP tools / document bridge | `UseWPF` or Scintilla on Logging / Telemetry / Hosting |
| Add `Hosting.<Host>` + `AddXxxLaunch()` (path / args / dialog); later `AddXxxInProcess()` | `switch (HostApp)` in `HostLaunchService`, a second wait loop, `IHostPlugin`, or `WaitOutcome` in `Mcp.Server` / Runner |

File family stays closed: `FromExtension(".dwg")` yields `AutoCad`, never
Civil3D. Adding Plant3D does **not** retouch the extension map.

Do **not** invent `IHostPlugin` / MEF / a host registry assembly to avoid
touching the enum. An enum bump **plus** `AddXxxLaunch()` is the cheapest,
honest leak. `AddRevitLaunch()` is not a plugin registry.

### P3. UI-free — binding list at Accept

An assembly is UI-free if it references none of: `PresentationFramework`,
`WindowsBase`, `DevTools.UI`, `MahApps.Metro`, `ZLogger.Scintilla`.
`net*-windows` TFM is allowed (named pipes, Win32 launch). Runner’s
boundary test **also** forbids `Microsoft-WindowsAPICodePack-Shell` (step E;
not a UI framework, but it must not ride on NUnit).

**Must be UI-free when this ADR is executed (proof: assembly-reference
tests, same pattern as
`tests/DevTools.NUnit.Core.Tests/CoreAssemblyBoundaryTests.cs`):**

- `DevTools.Hosting`
- `DevTools.Hosting.Revit`, `DevTools.Hosting.Acad`
- `DevTools.Logging` (after H)
- `DevTools.Telemetry`
- `DevTools.Settings` (after J)
- `DevTools.NUnit.Core`, `Transport`, `Host`, `Runner`
- `DevTools.FileMetadata.Core` / `Revit` / `Acad`
- `DevTools.Mcp.Core`

UI-free is not “no `DevTools.Logging`”. `NUnit.Host` **must** keep headless
Logging (ZLogger). The test forbids WPF/Scintilla/UI, not ZLogger.

**Become UI-free as a consequence of J** (Catalog no longer pulls MahApps):

- `DevTools.Mcp.Catalog`, `Client`, `Adapter`, `Server`

**Not UI-free, and not claimed to be:**

- `RevitDevTool`, `AcadDevTool`, `DevTools.Daemon`, `DevTools.Presentation`,
  `DevTools.UI`
- In-host tool assemblies (`DevTools.Mcp.Revit` / `Mcp.Acad`). They are
  host adapters (`UseRevit` / Acad API). The `Mcp.*` prefix is not a
  neutrality claim.

### P4. Log contract vs log pane

MCP / NUnit / Telemetry / Hosting consume
`Microsoft.Extensions.Logging.Abstractions` (`ILogger<T>`).

`ILogger<T>` is the **code** contract (NUnit.Core, NUnit.Host types).
`DevTools.Logging` is the **provider** assembly (ZLogger). After identity
moves:

- **NUnit.Core / Transport** stay MEL-only. No Logging, no ZLogger package.
- **NUnit.Host** keeps headless Logging. Retarget `IHostAppInfo` to Hosting;
  do **not** drop the Logging ProjectReference. Do **not** add a second
  ZLogger PackageReference on Host — providers come from `DevTools.Logging`.
  Closing the gap: Host (or a headless test host) can call the headless
  `AddLoggingProvider` without Presentation. Live Revit/Acad already do this
  in `AddLoggingServices` before `AddNUnitHostServices`.
- **Runner** drops Logging (CLI / pipe client; not the in-process hot path).
  Optional later: headless Logging for Runner file logs. Not required here.
- **Mcp.Server** drops Logging for identity (keeps its own ZLogger package).
  Unifying Server onto headless `DevTools.Logging` is optional after H.

`DevTools.Logging` is the **provider** (ZLogger file/HTTP/trace bridge,
`LoggingConfiguration`, `LoggerTraceListener` on `System.Diagnostics.Trace`).
The Scintilla monitor and WPF `PresentationTraceSources` wiring belong with
the pane (`DevTools.Presentation`).

Do **not** add `DevTools.Logging.Abstractions` or `DevTools.Logging.Ui`
projects. MEL already is the abstraction. Presentation already is the UI
layer (`ILoggingService.HostElement` is already there).

**Allowed edge:** `Logging → Hosting` (consumes `IHostAppInfo` for file
names). **Forbidden:** Logging re-exports `HostApp` / `IHostAppInfo`.

### P5. Telemetry is tagged identity, not settings UI

`ITelemetry` + `SentryTelemetryService` stay in `DevTools.Telemetry`.
Construction needs a DSN, enable flag, and `IHostAppInfo`. Hosts that already
own `ISettingsService` read `EnableTelemetry` at the composition root and
pass values in.

`IHostAppInfo` comes from Hosting, not Logging.

`InstallationId` today calls `AppUtils.GetApplicationDataPath()` (nine lines
of special-folder + `CreateDirectory`). Inline that. Do not keep a Utilities
ProjectReference (WindowsAPICodePack) for it.

### P6. New projects are allowed; app-specific types do not live in Daemon

**Create a project** when the type is host-app-specific (Revit install map,
`.rvt` year policy, Acad product-id map). Put it in an existing host-specific
assembly if one already owns that job (`FileMetadata.Revit` = OLE parse only),
otherwise **add** `DevTools.Hosting.Revit` / `DevTools.Hosting.Acad`.

**Do not** dump those types into:

- `DevTools.Daemon` (composition root only — register, do not implement)
- `DevTools.Mcp.Server` (`LaunchHostTool` stays dumb)
- generic `DevTools.Hosting` (no Autodesk product strings, no `.rvt` policy)

Still reject empty pyramids: no `Hosting.Abstractions`, no `Logging.Abstractions`,
no `IHostPlugin`, no assembly-scan registration. Contracts live **in**
`DevTools.Hosting`. Each host assembly exposes explicit
`IServiceCollection.AddRevitLaunch()` / `AddAutocadFamilyLaunch()` (Speckle
`AddRevit()` / `AddAutocadBase()` shape). In-process policy is a separate
`AddRevitInProcess()` / `AddAutocadInProcess()` (D2). Composition roots
**call** those methods; they do not discover plugins.

`DevTools.Hosting.Revit` must **not** ProjectReference `FileMetadata.Revit`.
Runner needs Revit path resolution without OpenMcdf. File-aware launch takes a
`Func<string, string?>` (or a small reader interface in Hosting.Revit);
**Daemon wires** `RevitFileMetadataReader.TryReadRevitVersion` at composition.
That wiring is not a `Revit*` type under `Daemon/Hosting/`.

## Decision

### 1. One process-identity vocabulary

Keep **`HostApp`** as the only product enum (Revit, AutoCad, Civil3D, …).

Keep **`IHostAppInfo`** as the in-host “who am I” contract (host, year, PID).

Delete **`FileHostApplication`**. `FileInfoResult` uses `HostApp`.

**File-level rule:** mapping from extension/path may only yield a **family**
head: `Revit` or `AutoCad` (and later `Navisworks` if we parse those files).
It must **not** infer `Civil3D` / `Plant3D` / `AcadMep` from `.dwg`. Those
values are for running processes (pipe name, registry product id, launch
request).

JSON wire for `read_file_info` already uses `"hostApp"`. Serializing
`HostApp.Revit` / `HostApp.AutoCad` keeps the same strings.

### 2. One extension map, next to `HostApp`

`HostApp.FromExtension(".rvt")` / `IsAcadFamily()` live **once**, beside the
enum, created in **step A**. Delete the copies on `AcadPathResolver` and
`Mcp.Server.Utils` in **A** (not D), or the merged Hosting assembly hits
CS0121.

`IFileReader.SupportedExtensions` stays reader-specific. MCP `launch_host` /
`open_document` use the shared map.

AutoCAD **product-id → HostApp** stays with install detection
(`AcadPathResolver.ProductIdMap` and in-host `AcadHostAppInfo`). Do not
delete `ProductIdMap` when deleting `IsAcadFamily`. Sharing one constant
between resolver and `AcadHostAppInfo` is optional later, not Accept-blocking.

### 3. Bounded contexts — do not merge them

```text
Identity     DevTools.Hosting          HostApp, IHostAppInfo, FromExtension, IsAcadFamily,
                                       IHostLaunchService, IHostPathResolver,
                                       generic CreateProcess + stdio + dialog resolver,
                                       HostLaunchWait / HostReadyStatus (one wait loop)
Revit launch DevTools.Hosting.Revit    RevitPathResolver, FindCompatibleVersion,
                                       RevitFileAwareHostLaunchService
                                       (no FileMetadata ProjectReference)
Acad launch  DevTools.Hosting.Acad     AcadPathResolver, ProductIdMap
Files        DevTools.FileMetadata.*   IFileReader, OLE/DWG parsers, catalog DI
Headless log DevTools.Logging          ZLogger file/HTTP, LoggingConfiguration,
                                       LoggerTraceListener on Trace.Listeners
                                       no UseWPF, no Scintilla, no PresentationTraceSources
Pane log     DevTools.Presentation     IMonitorLogTarget, MonitorLogTarget,
                                       monitor opt-in, WPF PresentationTraceSources half
Telemetry    DevTools.Telemetry        ITelemetry, Sentry; refs Hosting + Sentry only
Settings     DevTools.Settings         file configs; no DevTools.UI (after J)
Helpers      DevTools.Utilities        no FileMetadata, no Autodesk product strings
Compose      Daemon, Runner            register locators / file-aware decorator;
                                       no Revit* types under Daemon/
In-host MCP  DevTools.Mcp.Revit/Acad   host-bound in-host tools (UseRevit / Acad API)
```

**Do not** rename `FileMetadata.*` to `HostApp.*`. Parsing a `.rvt` without
the Revit API is not “host app infrastructure”.

**Do not** put Revit/Acad path resolvers or `.rvt` year policy in Daemon,
Mcp.Server, or generic Hosting. New `Hosting.Revit` / `Hosting.Acad` are the
intended homes. A later `Hosting.Tekla` is an add, not an edit of Hosting.

**Do not** put `HostApp` in `DevTools.Ipc` (pipe framing ≠ product identity)
or leave it in `DevTools.Logging` (even after Logging becomes headless:
identity ≠ logging).

### 4. `DevTools.Hosting` and per-host launch projects

Add **`source/DevTools.Hosting/`**: `net48;net8.0-windows;net10.0-windows`,
**no** `UseWPF`, **no** FileMetadata, **no** Shell dialogs, **no** Logging,
**no** Autodesk product strings.

`IHostLaunchService` + `IHostPathResolver` stay the composition point.
`HostLaunchService` starts a process given a resolver; it does not `switch`
on `HostApp.Revit` / `IsAcadFamily` for install paths.

Add **`source/DevTools.Hosting.Revit/`** and **`source/DevTools.Hosting.Acad/`**
in the same PR as the launch move (C+D):

- Revit: `RevitPathResolver`, `RevitArgumentBuilder`, `RevitStartupDialogSpec`,
  `FindCompatibleVersion(minimumYear)` (today’s oldest installed `>= fileYear`),
  file-aware decorator
- Acad: `AcadPathResolver`, `ProductIdMap`, `AcadArgumentBuilder`,
  `AcadStartupDialogSpec`

Shared-assembly types wait for **D2**. C+D does not touch add-in ALC hooks.

`Hosting.Revit` does **not** reference `FileMetadata.Revit`.
The file-aware decorator reads a year through
`Func<string, string?>` (or `IRevitDocumentYearReader` declared in
Hosting.Revit). **Daemon `ServerHostBuilder` wires**
`RevitFileMetadataReader.TryReadRevitVersion` — composition, not a type
named `RevitVersionResolvingHostLaunchService` under `DevTools.Daemon`.

`LaunchHostTool` stays dumb. Runner passes `filePath: null` and never
constructs the file-aware decorator, so it never needs FileMetadata.

Putting that decorator in Daemon was the wrong home (Daemon is a tray/MCP
host, not Revit launch policy). Putting it in FileMetadata.Revit would mix
OLE parse with `CreateProcess`. Both rejected.

**C and D ship as one PR** (generic Hosting launch + Hosting.Revit +
Hosting.Acad + Daemon wiring).

Oldest-PID reuse stays **NUnit Runner policy**, not Hosting.

### 4.1 Launch wait — one loop, pytest-style dialogs, per-host probes

Cold start is three jobs. Only the first two belong in generic Hosting; the
third is a **probe** the caller injects:

```text
Start          IHostLaunchService.Start → HostProcessStart (exe, args, PID)
Dialogs        StartupDialogResolverHandle — no self-timeout; lives for the wait
Ready wait     HostLaunchWait.UntilAsync(started, timeout, tryGetReady)
                 → HostReadyWaitResult<T> { Ready | Exited | TimedOut | Cancelled }
```

**Do not** give MCP, NUnit, or pytest each their own `WaitOutcome` / deadline
loop. `LaunchHostTool` stays dumb: spawn + `UntilAsync` + MCP-session probe.
Runner `HostSession` stays dumb on wait: spawn + `UntilAsync` + control-pipe
probe. Oldest-PID reuse remains Runner-only, before `Start`.

Dialog resolver matches pytest: poll until the wait returns (pipe/session
ready, process exit, launch timeout, or caller cancel). It does **not** use a
fixed 90s clock from `Process.Start`. Timeout is caller-owned
(`HostLaunchTiming.DefaultReadyTimeout` = 120s, same as pytest
`DEFAULT_LAUNCH_TIMEOUT_S`; NUnit `HostLaunchTimeout` may be longer).

`tryGetReady: int pid → T?` is the expansion joint. Hosting never names
`DevToolsMcp_*` or `DevTools_*`. MCP passes `IHostBroker.GetByProcessId`.
NUnit passes `HostLocator.Discover` filtered to the spawned PID. A later
host adds a probe, not a wait type.

Per-host path / args / dialog is **§4.2 C+D**. Shared-assembly is **§4.2 D2**.
Ready signal stays a caller probe, not a Hosting type.

Interim: `HostLaunchWait` / `HostReadyStatus` / `HostLaunchTiming` live in
`DevTools.Utilities/Hosting` and move with the rest of launch in **C+D**.

### 4.2 Host capability contracts — DI only, Speckle connector shape

Pattern taken from speckle-sharp-connectors: common interfaces in a shared
SDK; each host assembly implements them and exposes **explicit**
`IServiceCollection` extensions (`AddRevitLaunch()` / later
`AddRevitInProcess()`; Speckle’s `AddRevit()` / `AddAutocadBase()` shape).
Civil3D still calls the Acad-family launch method then adds extras if needed.
The composition root wires by **calling** those methods. Civil 3D does not
edit generic SDK code. Two methods per host (launch vs in-process) so add-ins
do not load registry scanners and Daemon does not register ALC policy.

Pattern **not** taken: `AddMatchingInterfacesAsTransient` (convention scan),
`IHostPlugin` / MEF, keyed-DI dictionaries of `HostApp`, or a second
`Hosting.Abstractions` project.

#### Launch contracts in `DevTools.Hosting` (C+D)

Three independent interfaces. `Supports(HostApp)` selects in **multi-host**
containers (Daemon, Runner). Implementations live only in `Hosting.<Host>`.

```text
IHostPathResolver              FindExecutable / GetInstalledVersions
IHostArgumentBuilder           Build(HostLaunchRequest) → argv  (required)
IHostStartupDialogSpec     Spec (window/button classes + that host’s
                               title/button catalog) or none (do not poll)
```

`HostLaunchRequest` is generic: `HostApp`, `Version`, `FilePath?`, plus an
opaque `IReadOnlyDictionary<string, string?> Options`. **Not** a
`LanguageCode` field.

Public language (MCP `launch_host` parameter + `LaunchHostResult.languageCode`
+ `Options["language"]`) is **.NET culture** (`CultureInfo.Name`: `en-US`,
`en-GB`, `fr-FR`, …). Default when omitted: `en-US`. Agents never pass
`ENU` / `PLNT3D` locale codes. The DTO **echoes the culture**, not the
host-native switch.

Each argument builder maps culture → that host’s CLI:

- **Revit** — ricaun [`RevitLanguageUtils`](https://github.com/ricaun-io/ricaun.RevitTest/blob/master/ricaun.RevitTest.Console/Revit/Utils/RevitLanguageUtils.cs)
  (`en-US` → `ENU`, `en-GB` → `ENG`, …). Do **not** copy ricaun’s `zh-CH`
  key (`zh-CN` → `CHS`). Keep `HUN`. Argv: `/language ENU` (this machine’s
  shortcuts). Unmapped culture throws.
- **Acad family** — culture is already what `acad.exe` wants
  (`/language en-US`). Identity map. Passing `ENU` into acad.exe is a bug
  in the mapper, not a public option.

Tekla/Navisworks flags go in `Options` or in that host’s builder; they must
not grow fields on the generic record. `HostProcessStart` drops the
Revit-shaped `LanguageCode` field. Runner omits language (builder default
`en-US`); it does **not** hardcode `"ENU"`.

Generic `HostLaunchService` does:

```text
path = paths.SingleOrDefault(p => p.Supports(host))
        ?? throw not-supported
args = arguments.SingleOrDefault(a => a.Supports(host))
        ?? throw not-supported   // never ?? []
        .Build(request)
dialogs = dialogs.SingleOrDefault(d => d.Supports(host))
validate file exists if FilePath set (before CreateProcess)
StartProcess(path, args)
start dialog handle with strategy.Options when present
```

A forgotten `Add*` for args must fail like a missing path resolver. Silent
empty argv would launch Revit without `/language` and without the model
file, then report bridge-connected success.

Constructor injection only:

```csharp
public HostLaunchService(
    IEnumerable<IHostPathResolver> paths,
    IEnumerable<IHostArgumentBuilder> arguments,
    IEnumerable<IHostStartupDialogSpec> dialogs)
```

Two implementations that `Supports` the same `HostApp` is a composition bug.
Test the **shared helper both roots call** (`AddHostLaunchCore` + the host
`Add*` methods), not an invented fourth container.

`Supports()` is justified because Daemon/Runner are **multi-host** processes
(Speckle’s `AddRevit()` never coexists with `AddAutocadBase()` in one
container; ours do). Linear scan of a handful of implementations is enough.
Keyed DI is not worth it.

Dialog **engine** in generic Hosting: `EnumWindows` / `SendMessageTimeout`
only. No default title or button lists. `StartupDialogResolverOptions` is a
parameter object filled from the strategy spec — not a class with Autodesk
strings. Timing (`PollInterval`, `ClickTimeout`) may stay engine-owned.

Each host owns a **closed catalog**. Do not add keywords. Do not merge
Revit and Acad into one bag. Core does not `Contains` a shared Autodesk
soup; it matches **only** the spec it was given (`Contains`, ignore-case,
because the live window title is longer than the fragment).

| Host | Title fragment (one) | Preferred | Blocked |
|------|----------------------|-----------|---------|
| Revit | `unsigned add-in` | `always load` | `do not load`, `load once` |
| Acad family | `unsigned executable file` | `always load` | `do not load`, `load once` |

Drop from today’s merged bag: `questionable add-in`, `cancel`, `no`.
Preferred/blocked strings are **duplicated on each spec**, not a core
default — identical text is not shared state.

`HostLaunchCoordinator` does not `switch` / `IsAcadFamily()`. No strategy
⇒ no poll. `StartupDialogResolverHandle` takes the strategy spec (today it
hardcodes `new StartupDialogResolverOptions()`).

Do **not** use a `Func<hwnd, bool>` — this is data, not an OLE-style
capability leak. Window/button class names (`#32770`, `Button`) live on
the spec, not as constants in generic Hosting after C+D.

#### Shared-assembly contract (D2, after E — not C+D)

`IHostSharedAssemblyPolicy` is **not** a launch dependency and must not
ride in C+D. Today `HostSharedAssemblies.IsShared` is called from static ALC
hooks (`CommandLoadContext.Load`, `HostAssemblyResolver` resolve events,
`DirectoryAssemblyLoad`, `NUnitSharedAssemblyPolicy` and eight NUnit.Host
loaders). None of those can take constructor DI. `Configure(hostApiDirectory)`
has **no caller** — the hardcoded seven-name set is the whole mechanism, not
a bootstrap.

D2 (after E, when the MahApps WPF ride is gone):

| Policy | Owner | Examples |
|--------|--------|----------|
| Host API | `IHostSharedAssemblyPolicy` in `Hosting.<Host>` | `RevitAPI`, `acmgd` |
| `Autodesk.` prefix | **both** host policies on purpose | `Autodesk.Revit.*` / `Autodesk.AutoCAD.*` |
| Framework | generic Utilities.AssemblyLoading | `System.`, `mscorlib` |
| Add-in UI packages | **one** owner: Execution (`HostPackagePrefixes`); NUnit.Host keeps calling through it | `MahApps.`, `ControlzEx.`, `CommunityToolkit.` |

In-process policy is a **plain singleton without `Supports()`** — the add-in
container has one host. Consumption is an ambient set-once at add-in
startup (`HostSharedAssemblies.Use(policy)` or equivalent) that the existing
static ALC hooks read. That is honest: we are not pretending eight static
entry points become DI. Do **not** redesign NUnit.Host loaders in C+D.

Do **not** claim directory scan as a fallback until something actually calls
`Configure`.

#### `Add*` modules (the only wiring)

Launch (C+D) — two extensions per host so add-ins do not load registry
scanners:

```csharp
services.AddHostLaunchCore();          // engine, wait, Win32 poller
services.AddRevitLaunch(readDocumentYear: null);
// Daemon: AddRevitLaunch(RevitFileMetadataReader.TryReadRevitVersion)
services.AddAutocadFamilyLaunch();     // Supports() all Acad-family HostApp
```

In-process (D2):

```csharp
services.AddRevitInProcess();          // IHostSharedAssemblyPolicy + Use(policy)
services.AddAutocadInProcess();
```

Composition roots (no `new HostLaunchService()`):

| Root | C+D | D2 |
|------|-----|-----|
| Daemon `ServerHostBuilder` | Core + `AddRevitLaunch(OLE)` + `AddAutocadFamilyLaunch` | — |
| NUnit Runner | Core + `AddRevitLaunch(null)` + `AddAutocadFamilyLaunch` via `ConsoleApp.ServiceProvider` in `Program.Main`; `NUnitRunnerCommands(HostSession)` | — |
| `RevitDevTool` / `AcadDevTool` | **do not** reference launch extensions | `AddRevitInProcess` / `AddAutocadInProcess` |
| NUnit.Host | unchanged | reads ambient policy |

Runner is `PublishSingleFile` / `SelfContained`. C+D adds
`Microsoft.Extensions.DependencyInjection` to its allowed references
(§10 + boundary test).

`Mcp.Server` still takes `IHostLaunchService` from the Daemon container. It
does not reference `Hosting.Revit` / `Hosting.Acad`.

C+D keeps **one** `Hosting.Acad`. Civil 3D argv is not vanilla `acad.exe`.
That table lives in `AcadArgumentBuilder` (or a Civil-specific builder in
the same project). Generic `HostLaunchService` still has no `switch`.

`Hosting.Civil3d` + `AddCivil3dLaunch()` is a **later extract**, not C+D.
The seam is already `IHostArgumentBuilder` — splitting must not touch
generic Hosting, Daemon wait, or MCP `LaunchHostTool`. Do not invent the
project until Civil argv/dialogs need their own csproj.

C+D argv is the **shortcuts we have**, not the full Autodesk switch bible.
Do **not** emit `/nologo`.

| HostApp (live on this machine) | Argv |
|--------------------------------|------|
| Civil3D 2026 | `/ld {install}\AecBase.dbx` `/p <<C3D_Metric>>` `/product C3D` `/language en-US` |
| Plant3D 2027 | `/product PLNT3D` `/language en-US` |
| Revit 2022–2027 | `/language ENU` |

Other Acad-family `HostApp` values: `/product {code}` + `/language en-US`
only (`ACAD`, `MAP`, `ACA`, `ACADM`, `MEP`, `ACADE`). **Do not** invent
`/ld` / `/p` for Arch/MEP. Imperial Civil (`<<C3D_Imperial>>`) is a later
one-liner on the Civil builder, not C+D.

`/ld` path = `Path.Combine(GetDirectoryName(acad.exe), "AecBase.dbx")`.
Missing dbx ⇒ fail Civil launch.

Proof Civil launched as C3D: pipe `DevTools_Civil3D_2026_*`, not
`DevTools_AutoCad_*`.

`Options` keys (C+D): `"language"` = .NET culture only. Callers omit it to
take `en-US`. MCP `launch_host.languageCode` is that culture (tool
description must not say “Revit ENU”). `LaunchHostResult.languageCode`
echoes the culture. `HostLaunchRequest` has no `LanguageCode` field.

Adding Tekla = `Hosting.Tekla` + `AddTeklaLaunch()` + `HostApp.Tekla` —
zero edits to `HostLaunchService` or `HostLaunchRequest` fields.

#### Forbidden

- `switch (hostApp)` / `IsAcadFamily()` inside `HostLaunchService` after C+D
- `IHostPlugin`, MEF, `AddMatchingInterfacesAsTransient`
- Keyed services as the primary selector
- `?? []` for a missing argument builder
- `LanguageCode` on generic `HostLaunchRequest` / `HostProcessStart`
- Product dialog strings or `#32770` as defaults in generic Hosting
  (`unsigned add-in`, `unsigned executable file`, `questionable add-in`,
  `always load`, `do not load`, `load once`)
- Merged Autodesk keyword bag / union-test across hosts
- `HostLaunchCoordinator` gating on `IsAcadFamily()` after C+D
- Folding the shared-assembly split into C+D
- MahApps/ControlzEx prefixes on `Hosting.Revit`
- Add-ins calling `AddRevitLaunch()` (would pull install-path code into Revit)
- `/nologo` / `/nosplash` on launch argv (not in the provided shortcuts)
- Documenting or accepting Revit `ENU` on the MCP `languageCode` parameter
- Echoing `ENU` on `LaunchHostResult` (echo culture, e.g. `en-US`)
- Inventing Arch/MEP `/p` profile names in C+D

### 5. Utilities becomes helpers again

After **E**, `DevTools.Utilities` has no FileMetadata, no `Hosting/` folder,
no Autodesk product strings, no `UseWPF`, no `DevTools.Logging`, and no
Shell package.

NUnit.Host uses Utilities only for `AssemblyLoading` (`NUnitRuntimeLoadContext`
etc.). Today Utilities compiles WPF (`HostUiHelper`, `Win32Utils.SetHostAppOwner`)
**without** `UseWPF` by riding Logging’s `UseWPF`. After H, that ride
disappears — **E must land before H**.

E moves:

- `AppUtils.SelectFolder` + WindowsAPICodePack → Presentation
- `HostUiHelper` and WPF `Window` extensions → `DevTools.UI` (or Presentation)
- Raw non-WPF Win32 (dialog class names, `SendMessage` used by
  `StartupDialogResolver`) → Hosting with the resolver in **C+D**, not left
  as `internal` on Utilities

Win32 that mentions no product and no WPF may remain in Utilities. Hosting
must not take WPF helpers.

**UI-free for NUnit.Host is proven only after E+H**, not after A. Step A’s
acceptance text must say Host remains transitively WPF until then.

### 6. Agents rename is independent

`DevTools.Agents.Revit` → `DevTools.Mcp.Revit`, `Agents.Acad` →
`DevTools.Mcp.Acad`. Same assemblies, same host ProjectReferences, namespace
update. They remain host-bound adapters. Not a prerequisite for
identity/launch. Do not move launch or FileMetadata into those projects.
Do not treat the `DevTools.Mcp.*` prefix as “UI-free / host-neutral” for
these two.

### 7. Logging is headless; the pane (and WPF trace) move to Presentation

`DevTools.Logging` drops `UseWPF`, `ZLogger.Scintilla`, and `Scintilla5.NET`.

**Split, do not blindly move, `TraceListenerHelper`:**

- Stay in Logging: add/remove on `Trace.Listeners` (`LoggerTraceListener` is
  already headless).
- Move to Presentation: `PresentationTraceSources.*` switches and listeners
  (`ApplyPresentationTraceSwitches`, `GetWpfTraceSources`). Every current
  caller is a host or Presentation.

**Pane types move to Presentation** (Presentation already references Logging
and UI; `ILoggingService` already exposes `FrameworkElement? HostElement`):

- `IMonitorLogTarget`
- `MonitorLogTarget`
- Scintilla registration (`AddMonitorLogging`)

**Split `AddLoggingProvider`:** headless builder registers configuration +
notify + file + HTTP. Hosts that want the pane call a Presentation-side
opt-in (monitor + WPF trace). The `Action<ScintillaOptions>?` parameter
leaves Logging.

**net48 native DLLs:** `Scintilla5.NET` currently copies unmanaged DLLs for
`net4*`. That condition must live on Presentation (or the host csproj) so
Revit 2022–2024 / net48 AutoCAD still get the pane. Step H is not proven by
a net8 host build alone.

Gate for H: **`DevTools.Logging` compiles with `UseWPF` removed**, including
a **net48** host configuration that still shows the pane.

### 8. Telemetry graph

After identity move, `DevTools.Telemetry` ProjectReferences:

```text
DevTools.Hosting
Sentry
Microsoft.Extensions.* (as today)
```

**Forbidden:** `DevTools.Settings`, `DevTools.UI`, `DevTools.Logging`,
`DevTools.Utilities`.

Replace `TelemetryServiceRegistration.Resolve`’s `ISettingsService` lookup
with parameters (or a small options type) supplied by the host/Daemon
composition root: `enable`, `dsn`, `IHostAppInfo`. NoOp remains the fallback.

### 9. Settings drops UI (so MCP can)

`GeneralConfig.Theme` stops using `DevTools.UI.Theme.AppTheme`. Put a
Settings-owned enum in `DevTools.Settings.Configs` with the **same numeric
values** (`Light = 0`, `Dark = 1`, `Auto = 2`). `FileConfig` uses default
`JsonSerializerOptions` (no string-enum converter); existing settings files
store `"theme": 0` not `"Light"`. Names matching is not enough — preserve
ordinals. Add a golden JSON fixture before switching the type.

Presentation / hosts map that enum onto `DevTools.UI.Theme.AppTheme` at the
VM/host edge. `ThemeManager` stays in UI. Then Settings drops `DevTools.UI`.
Settings may keep **headless** Logging for `LogConfig` option types.

### 10. Runner closure

After A–D, Runner’s intended references are:

```text
NUnit.Core, NUnit.Transport, Ipc, Hosting, Hosting.Revit, Hosting.Acad
(+ Interop only for VS Debug attach)
```

Hosting.Revit here is **path / args / dialog** at runtime for Runner
(`filePath: null`, no OLE). Shared-assembly policy is D2 and does not
register in Runner. Runner builds the collection in `Program.Main` via
`ConsoleApp.ServiceProvider` (or equivalent) and calls
`AddHostLaunchCore()` + `AddRevitLaunch(null)` + `AddAutocadFamilyLaunch()`.
`NUnitRunnerCommands` takes `HostSession` from DI; it must **not**
`new HostLaunchService()`. Daemon additionally passes `TryReadRevitVersion`
into `AddRevitLaunch`. C+D adds `Microsoft.Extensions.DependencyInjection`
to Runner’s allowed references (PublishSingleFile / SelfContained still
holds).

It must **not** reference FileMetadata, WindowsAPICodePack, Presentation,
DevTools.UI, or Logging (WPF or headless). Runner is out-of-process; ZLogger
performance belongs on **NUnit.Host**, not on the CLI. This does not change
0016 decision 11.

### 11. Dependency direction (after)

```text
Hosts, Daemon, Presentation
  -> Logging (headless) + Presentation monitor + UI + Telemetry + Hosting + Settings

Daemon
  -> Hosting + Hosting.Revit + Hosting.Acad + FileMetadata.Revit (wire only)

Logging -> Hosting          (IHostAppInfo only; no re-export)
Telemetry -> Hosting
Settings -> Logging         (option types; headless)
FileMetadata.Core -> Hosting
Hosting.Revit -> Hosting    -X-> FileMetadata
Hosting.Acad  -> Hosting

Runner
  -> Hosting + Hosting.Revit + Hosting.Acad
  -X-> Logging, UI, Presentation, FileMetadata, Settings

NUnit.Host
  -> Hosting + headless Logging (ZLogger providers)
  -X-> UI, Presentation, FileMetadata, Settings, ZLogger.Scintilla

Mcp.Server
  -> Hosting, FileMetadata.Core, MEL / own ZLogger
  -X-> FileMetadata.Revit, Hosting.Revit, Hosting.Acad, UI, Presentation
  (Catalog → Settings is OK after J)

Hosting  -X->  FileMetadata, Logging, UI, Hosting.Revit
Daemon   -X->  Revit* launch types in its own source tree
Telemetry -X-> Settings, UI, Logging, Utilities
Logging  -X->  UI, Scintilla, PresentationTraceSources
Utilities -X-> FileMetadata, Autodesk strings
```

## Implementation sequence (review this order)

Each step is a separate PR unless a step is empty. Compile the touched csproj
per the build skill. Do not mix Agents rename with the Hosting move.

| Step | Change | Proof | Legacy removed |
|------|--------|--------|----------------|
| **A** | Create `DevTools.Hosting` with `HostApp`, `IHostAppInfo`, `FromExtension`, `IsAcadFamily`. Retarget usings. Delete `IsAcadFamily` / `FromExtension` copies on `AcadPathResolver` and `Mcp.Server.Utils` **in this PR** (keep `ProductIdMap`; `FromPipeName` / `ParseHostApp` may stay as thin wrappers). Mcp.Server and **Runner** drop Logging; take Hosting. **NUnit.Host keeps Logging** and adds Hosting for `IHostAppInfo`. Logging references Hosting for `FileLogProcessor`. | `dotnet build` Hosting + Logging + FileMetadata.Core + Runner + NUnit.Host | Duplicate `IsAcadFamily` / `FromExtension`; Runner → Logging; NUnit.Host `using DevTools.Logging` for identity |
| **B** | Delete `FileHostApplication`. `FileInfoResult.HostApplication` is `HostApp`. Update MCP contract tests. | `DevTools.Mcp.Tests` FileInfo fixtures | `FileHostApplication` |
| **C+D** | One PR. Stop `Utilities → FileMetadata.Revit`. Generic launch + **three** contracts (`IHostPathResolver`, `IHostArgumentBuilder`, `IHostStartupDialogSpec`) + `AddHostLaunchCore()` → `DevTools.Hosting`. `AddRevitLaunch` / `AddAutocadFamilyLaunch` register implementations. `HostLaunchService` takes `IEnumerable<T>` + `Supports(HostApp)` — no `switch`, no `?? []` for args. Opaque `Options` bag on `HostLaunchRequest` (no `LanguageCode`). Daemon calls Core + both `Add*Launch` (OLE func into `AddRevitLaunch`). Runner uses `ConsoleApp.ServiceProvider` the same way (no OLE, MEDI allowed, no `new HostLaunchService()`). Add-ins **do not** reference launch extensions. Do **not** split `HostSharedAssemblies`. | HostSessionPolicyTests; HostLaunchWaitTests; Hosting.Revit.Tests compatible-year + language (culture `en-US` → argv `ENU`; MCP DTO echoes `en-US`; reject unmapped culture / reject `ENU` on MCP); Hosting.Acad.Tests golden argv (Civil3D = `/ld` + `/p <<C3D_Metric>>` + `/product C3D` + `/language en-US`; Plant3D = `/product PLNT3D` + `/language en-US`; **no** `/nologo` / `/nosplash`); HostLaunchService source has no `switch (HostApp`; at-most-one Supports on the shared `Add*` helper both roots call; dialog Revit catalog is only `unsigned add-in` + blocked `do not load`/`load once`; Acad family only `unsigned executable file` + same blocked pair; generic Hosting has no product dialog strings; Mcp.Server has no FileMetadata.Revit; Runner has no OpenMcdf; Hosting.Revit/Acad **net48 TFM** via `-c Debug`; Daemon `launch_host` with a 2025 `.rvt` still picks installed 2026 if that is the oldest `>= 2025`; **live CLI** (this machine): Civil3D 2026 pipe `DevTools_Civil3D_2026_*`, Plant3D 2027 pipe `DevTools_Plant3D_2027_*`, Revit 2022–2027 `/language ENU` | OpenMcdf on Runner; `Utilities/Hosting/`; `HostLaunchService.Resolve` switch; `new HostLaunchService()` in Runner; `LanguageCode` on generic launch types; Revit launch policy in Daemon; `/nologo`-only Acad argv; private `WaitOutcome` / wait loops |
| **E** | Move `SelectFolder` + Shell to Presentation. Move `HostUiHelper` and WPF `Window` extensions to UI. Drop Utilities → Logging and Shell. Confirm Utilities has no Autodesk strings, no `UseWPF`, no FileMetadata. **Must land before H.** | Utilities + NUnit.Host compile on **net48** without PresentationFramework in NUnit.Host’s *direct* graph after this PR (full UI-free gate still waits for H). Folder pickers still work in Presentation | Shell on Runner/Utilities; WPF types in Utilities |
| **D2** | After E. `IHostSharedAssemblyPolicy` in `Hosting.<Host>`; `AddRevitInProcess` / `AddAutocadInProcess`. Ambient `HostSharedAssemblies.Use(policy)` at add-in startup — static ALC hooks keep calling `IsShared`. MahApps/ControlzEx/CommunityToolkit: **one** owner in Execution. `Autodesk.` duplicated on both host policies on purpose. Do not redesign NUnit.Host loaders. Do not treat `Configure(directory)` as a fallback until it has a caller. | Add-in DI registers the singleton (no `Supports()`); `IsShared("RevitAPI")` / `acmgd` still true; generic Hosting has no MahApps strings; Execution owns UI-package prefixes | Host-API names (`RevitAPI`, `acmgd`, …) on static `HostSharedAssemblies`; add-ins hand-registering policy types |
| **H** | Split `TraceListenerHelper`. Move Scintilla monitor types and `AddLoggingProvider`’s monitor parameter to Presentation. Drop `UseWPF` / Scintilla packages from Logging; keep `Scintilla5.NET` copy on Presentation or net48 hosts. NUnit.Host still references Logging and still receives ZLogger via the host’s headless `AddLoggingProvider` (no pane required). | Logging compiles **without** `UseWPF` on **net48**; pane still works on a net48 host (R22–R24 or net48 Acad); NUnit.Host compiles with Logging and without Presentation | Scintilla / `FrameworkElement` / `PresentationTraceSources` in Logging |
| **I** | Telemetry refs Hosting + Sentry only. Composition root passes enable/DSN/`IHostAppInfo`. Inline `GetApplicationDataPath`. | Telemetry csproj; host still records MCP/execution when enabled | Telemetry → Settings / Logging / Utilities |
| **J** | Settings-owned theme enum; drop Settings → UI. | Settings.csproj has no DevTools.UI; Mcp.Catalog closure has no MahApps / PresentationFramework | Settings → UI; MCP MahApps chain |
| **F** | Rename `Agents.*` → `Mcp.Revit` / `Mcp.Acad`. Update host DI FQNs. Assemblies stay host-bound. | Host compile R25 + Acad 2026 | `DevTools.Agents.*` names |
| **G** | Update **one** doc layer: `docs/agents/host-boundaries.md`. Add UI-free + identity-home axes; do **not** retract 0002 host-neutrality. Link this ADR. Add the one-line 0007 clarification for in-host MCP tools. | Doc-only | Missing axes in the agent map |

Prefer **A, B, C+D, E, D2, H, I, J**, then optional **F**, then **G**.
**E before H is a compile constraint** (Utilities WPF ride on Logging).
**D2 after E** so MahApps prefix ownership is not still riding Utilities WPF.

Do not start **F** before **A** if both touch host csproj in the same week.
**F is not an Accept condition** — rename only, after the graph is stable.

## Legacy to delete (checklist)

- [x] `FileHostApplication` and its JSON converter usage
- [x] `IsAcadFamily` / `FromExtension` on `AcadPathResolver` **and** `Mcp.Server.Utils` (step A; keep `ProductIdMap`; `FromPipeName` / `ParseHostApp` may stay)
- [x] `Utilities` → `FileMetadata.Revit` ProjectReference
- [x] `HostLaunchService` `using DevTools.FileMetadata.Revit`
- [x] `HostLaunchService.ResolveAcad` `/nologo` argv (C+D; replace with
      provided Civil/Plant shortcuts)
- [x] `new HostLaunchService()` in `NUnitRunnerCommands`
- [x] `LanguageCode` on generic `HostLaunchRequest` / `HostProcessStart`
- [x] Merged `StartupDialogResolverOptions` defaults (`unsigned add-in` +
      `unsigned executable file` + `questionable add-in`; blocked `cancel` /
      `no`); `HostLaunchCoordinator` `IsAcadFamily` dialog gate
- [x] Host-API names (`RevitAPI`, `acmgd`, …) on static `HostSharedAssemblies` (**D2**)
- [x] `DevTools.Utilities/Hosting/` after the move
- [x] Any `Revit*` launch type under `source/DevTools.Daemon/`
- [x] `HostApp` / `IHostAppInfo` definitions in `DevTools.Logging`
- [x] Mcp.Server → Logging; Runner → Logging (step A, after Hosting exists). **Keep** NUnit.Host → Logging.
- [x] `UseWPF`, `ZLogger.Scintilla`, `Scintilla5.NET` on `DevTools.Logging`
- [x] `IMonitorLogTarget` / `MonitorLogTarget` / Scintilla-typed `AddLoggingProvider` in Logging
- [x] WPF half of `TraceListenerHelper` in Logging
- [x] Telemetry → Settings, Logging, Utilities
- [x] Settings → `DevTools.UI`
- [x] `DevTools.Agents.Revit` / `Acad` project names (step F)
- [x] Folder picker in Utilities if Presentation owns UI

**Keep:** FileMetadata parsers, `IFileReader` catalog, pipe name format,
`IHostLaunchService` (no oldest-PID policy inside it), the three launch
contracts + `AddXxxLaunch()` (no `IHostPlugin`), `HostSharedAssemblyNames`
+ `HostSharedAssemblies.Use` at add-in startup, `HostLaunchWait` / `HostReadyStatus` (one
wait loop; caller probe), EnvDTE in Runner, dialog resolver Win32 engine
(no self-timeout; lifetime = wait; **no default** product catalogs or class
names — spec comes from `IHostStartupDialogSpec`),
`ITelemetry`, ZLogger file/HTTP providers, MEL as the log contract,
`AcadPathResolver.ProductIdMap`, `LoggerTraceListener` on `Trace.Listeners`.

## Alternatives Considered

1. **Put `HostApp` on FileMetadata.Core** — inverts the graph (launch/NUnit
   depend on file parsers). Rejected.
2. **Empty `Hosting.Abstractions` + plugin registry** — rejected. Per-host
   **`DevTools.Hosting.Revit` / `Acad`** (real path + year policy) is the
   allowed extra-project cut. A later Tekla host adds `Hosting.Tekla`.
3. **Merge FileMetadata into Hosting** — mixes OLE parsing with
   `CreateProcess`. Daemon wires both; Runner must not take OLE. Rejected.
4. **Leave `HostApp` in Logging and `#if` WPF** — Runner still links a WPF
   logging assembly. Rejected as the identity home.
5. **`DevTools.Logging.Abstractions` + `Logging.Ui`** — MEL already is the
   abstraction; Presentation already is the UI home. Rejected.
6. **`IHostPlugin` / registry so `HostApp` never gains members** — hides a
   one-line enum bump. Rejected.
7. **Keep Telemetry → Settings; only move `AppTheme`** — still couples Sentry
   construction to Settings. Composition-root parameters (I) plus Settings
   dropping UI (J) are both required: I for Telemetry, J for MCP.
8. **Let `LaunchHostTool` call FileMetadata.Revit** — shorter diff, illegal
   under §11. Rejected.
9. **`RevitVersionResolvingHostLaunchService` in `DevTools.Daemon`** —
   composition root owning Revit launch policy. Rejected. Type lives in
   `DevTools.Hosting.Revit`; Daemon only registers it.
10. **`Hosting.Revit` ProjectReferences `FileMetadata.Revit`** — Runner would
    take OpenMcdf again. Rejected. File-aware ctor takes a func/reader;
    Daemon wires OLE.
11. **Rename in-host tools to `RevitDevTool.Mcp` / `AcadDevTool.Mcp`** —
    clearer 0007, worse MCP catalog naming. Keep `DevTools.Mcp.Revit` as a
    host-bound exception (P3 list) instead of a second rename.
12. **Split Execution off `DevTools.UI` in this ADR** — real
    (`TreeNodeBase`), in-host UI state, not the MCP/NUnit/Logging graph.
    Out of scope.
13. **Drop NUnit.Host → Logging because it has no ZLogger PackageReference** —
    treats a missing high-perf sink as unused identity. Rejected. Host keeps
    headless Logging; ZLogger stays the in-process provider.
14. **NativeAOT / PublishTrimmed as a design driver** — rejected. Module
    boundaries are the reason.
15. **Private `WaitOutcome` / wait loop in `LaunchHostTool` (and a second
    copy in Runner `HostSession`)** — looks local, duplicates clocks, and
    dies when a third host or pytest-style timeout is added. Rejected.
    One `HostLaunchWait.UntilAsync` + caller probe. Dialog resolver has no
    self-timeout (pytest); pipe/session wait remains the safety valve.
16. **Keyed DI `AddKeyedSingleton<IHostPathResolver>(HostApp.Revit, …)`** —
    eight keys for one Acad-family resolver. `Supports(HostApp)` +
    `IEnumerable<T>` is enough. Rejected.
17. **`AddMatchingInterfacesAsTransient` (Speckle convention scan)** —
    registers every interface in the assembly. Too implicit for three
    launch contracts. Explicit `AddRevitLaunch()` only. Rejected.
18. **`IHostPlugin` / MEF so Daemon never lists hosts** — hides the enum
    bump and the `Add*` call. Composition roots must stay readable.
    Rejected (same as alternative 6).
19. **Fold `IHostSharedAssemblyPolicy` into C+D** — looks tidy on paper.
    Callers are static ALC hooks with no DI. `Configure(directory)` has no
    caller. Rejected. D2 after E, ambient `Use(policy)`.
20. **`LanguageCode` on generic `HostLaunchRequest`** — forces a Hosting
    field edit for every Revit-shaped flag (Tekla, Navisworks). Opaque
    `Options` bag. Rejected.
21. **Merged Autodesk dialog bag in generic `StartupDialogResolverOptions`**
    (`unsigned add-in` + `questionable add-in` + `unsigned executable file`,
    blocked `cancel`/`no`) — forgiveness across hosts. Rejected. Closed
    per-host catalog; engine has empty defaults. `Func<hwnd, bool>` rejected
    (data, not an OLE leak).
22. **Emit `/nologo` (or `/nosplash`) because unattended “should” hide splash**
    — not in the provided shortcuts. Rejected. Identity switches only.
23. **MCP `languageCode` is Revit `ENU` (today’s tool description)** —
    agents should not learn a per-host alphabet. Public value is
    `CultureInfo.Name`; builders map. Rejected as the agent-facing contract.
24. **`Hosting.Civil3d` in C+D** — allowed later; the argument-builder seam
    makes the extract local. C+D stays one `Hosting.Acad`. Rejected as a
    C+D project.

## Consequences

Positive:

- One enum and one extension map.
- Runner launch no longer pays for OpenMcdf or WPF logging.
- NUnit.Core/Runner and Mcp.Core take identity + `ILogger<T>` without WPF.
  NUnit.Host keeps headless ZLogger via `DevTools.Logging`. After J,
  Mcp.Server/Catalog also lose MahApps.
- Telemetry no longer implies a settings window.
- `host-boundaries.md` can name three axes: host-API-neutral, UI-free,
  identity home.
- Adding a host is: enum member + `DevTools.Hosting.<Host>` +
  `AddXxxLaunch()` (path, args, dialog) + composition-root one-liner, then
  `HostSharedAssemblies.Use(HostSharedAssemblyNames)` in that host’s
  `Application.OnStartup` when the ALC name list exists. Not a rewrite of
  generic Hosting, Logging, NUnit.Core, or a new wait loop in MCP/Runner.

Tradeoffs:

- Three new csproj (`Hosting`, `Hosting.Revit`, `Hosting.Acad`) and slnx
  entries.
- FileMetadata.Core references generic Hosting (identity; unused generic
  launch types in that DLL are acceptable).
- `launch_host` with a file path uses `RevitFileAwareHostLaunchService`;
  Daemon only wires the OLE reader.
- Large mechanical `using` updates for `HostApp`.
- Hosts must opt into monitor + WPF trace after Logging stops doing it.
- In-host `AcadHostAppInfo` product map may still duplicate `ProductIdMap`.
- Execution still references `DevTools.UI`. That remains leftover, not
  Accept proof.
- Generic Hosting now owns `Process.Start` and Win32 dialog polling.
  Identity consumers of Hosting do not use those types; the widened surface
  is the launch engine, not `HostApp`.
- Shared-assembly remains ambient (`Use(HostSharedAssemblyNames)`), not constructor DI, because
  ALC resolve events are static. D2 does not pretend otherwise.
- Runner takes `Microsoft.Extensions.DependencyInjection` (C+D).

## Amendment (2026-08-17)

Independent graph review after D2/H/I. Keep six projects (three Hosting + three
FileMetadata). Do not merge FileMetadata into Hosting.Revit (Runner OpenMcdf).
Do not fold FileMetadata.Core into Hosting (`Mcp.Server` holds `IFileReader`
without parser packages).

Landed:

- `IHostSharedAssemblyPolicy` → `HostSharedAssemblyNames` record in Utilities.
- `HostSharedAssemblies.Use(names)` in `Application.OnStartup` next to
  `AssemblyLoader.Initialize()`. No `AddRevitInProcess` / `AddAutocadInProcess`
  (those were fake DI).
- `HostPackagePrefixes` moved to `Utilities/AssemblyLoading/`. Utilities is a
  leaf (no Hosting, no Execution.Abstractions).
- Dead `Utilities/Win32Utils`, `HostSharedAssemblies.Configure` / directory
  scan, and `HostAssemblyResolver.EnsureRegistered` removed. Command ALC still
  returns null for shared names.
- Add-in DI folder is `Composition/` (`RevitServiceRegistration`), not
  `Hosting/` (homonym with `DevTools.Hosting`).
- `AddAutocadFamilyLaunch` calls `AddHostLaunchCore()` so it is self-sufficient.
- MCP pipe/`hostApp` parsing is `HostAppParsing`, not a second `HostAppExtensions`.
- Planned **K** (`Utilities/Interop` creating `Hosting → Utilities`) is
  **rejected**. Launch P/Invoke stays in Hosting (`DialogNative`,
  `StdioInheritance`). WPF Win32 stays in `DevTools.UI`.

## Follow-Up

- Sequence A–J, G, and optional F are on `develop` (`a93e5f3d` for F).
  **K** rejected. Task memory:
  `docs/plans/completed/2026-08-15-host-identity-ui-free-infrastructure.md`.
- NativeAOT / `PublishTrimmed` is not a follow-up of this ADR.
- [0007](0007-revit-core-and-visualization-boundaries.md): one sentence —
  in-host MCP tool projects (`DevTools.Mcp.Revit` after F) may reference
  their host Core; they are not shared platform.
- Update `docs/ARCHITECTURE.md` module map when Hosting exists (one line +
  link here). Do not duplicate this ADR into architecture/.
