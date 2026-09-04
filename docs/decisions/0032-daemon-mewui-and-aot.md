# 0032 Daemon Desktop Is MewUI; Native AOT Is The Target

Date: 2026-09-04

## Status

Accepted — **MewUI desktop shell** (shipped). **Native AOT** is a documented
long-term target, not current production publish.

Companion to [0018](0018-host-identity-and-out-of-process-infrastructure.md)
(Runner AOT rejected — Daemon AOT is separate),
[0027](0027-mcp-product-surface.md) (MCP product surface on this process),
and [0031](0031-daemon-json-source-gen.md) (source-gen JSON so this AOT target is
reachable).

Living map: [`docs/architecture/MCP/daemon.md`](../architecture/MCP/daemon.md).

## Context

`DevTools.Daemon` is the standalone external MCP host: tray desktop, gateway
tunnel, control pipe, and `--stdio` MCP server. Before 2026-09 it used WPF
(MahApps dashboard, WPF `H.NotifyIcon`, XAML views). WPF ties the process to
`PresentationFramework` and blocks a closed Native AOT publish graph.

Separately, Daemon publish had been **self-contained ReadyToRun** single-file.
That drove a **>90 MB** exe and **>160 MB** RAM footprint. Moving to
**framework-dependent JIT** single-file cut size and memory sharply while still
shipping one `DevTools.Daemon.exe` that requires an installed **.NET 10**
runtime.

The UI rewrite and publish posture change are related but distinct: MewUI removes
the WPF dependency from the desktop shell; JIT publish is what ships today;
Native AOT is the reason for MewUI and the next publish milestone once
reflection-heavy closure work is proven.

A 2026-09-03 AOT spike (`dotnet publish` with `PublishAot`) produced a native
binary but surfaced architectural trim warnings; production cutover was rolled
back. Trust **`DevTools.Daemon.csproj` and `PublishDaemonModule`**, not stale
plan Status text that contradicts the rolled-back header.

## Decision

### 1. Daemon desktop UI is MewUI (Direct2D), not WPF — **Accepted / shipped**

Replace the WPF/MahApps dashboard and tray with:

- **MewUI** (`Aprillz.MewUI.Windows`, `MewUIBackend=Direct2D`) — C# markup
  views under `source/DevTools.Daemon/Views/` (`MainWindow`, `OverviewView`,
  `HostsView`, `SettingsView`).
- **Tray** — `H.NotifyIcon` **core** package (`H.NotifyIcon.Core`), not the WPF
  package; MewUI `ContextMenu` native popup for the right-click menu.
- **Auth browser** — `AuthBrowser` (loopback) instead of WPF `LoopbackBrowser`.
- **Desktop state** — `AppState`, `UserSettings` / `UserSettingsStore`,
  `ThemeHelper`, mutex `DevToolsDaemon_v1`.

WPF/XAML artifacts (`App.xaml`, `DashboardWindow.xaml`, `TrayResources.xaml`,
MahApps) are removed. `DevTools.Presentation` / host add-ins remain WPF; this
ADR is **Daemon-only**.

### 2. Production publish stays framework-dependent JIT — **Accepted / current**

Default `dotnet publish -c Release` (and `PublishDaemonModule`) uses:

| Flag | Value |
|------|--------|
| `SelfContained` | `false` |
| `PublishSingleFile` | `true` |
| `PublishReadyToRun` | not set (dropped) |
| `PublishAot` | not set |

Output is a **single-file exe that requires the .NET 10 runtime** on the
machine. This is production today.

### 3. Native AOT is the long-term target for standalone Daemon — **not production**

**Goal:** publish `DevTools.Daemon` as Native AOT once the closure is
AOT-compatible at runtime, not only at compile time.

**Not done:** `PublishAot` is absent from the default csproj; bundle deploy
always copies the JIT binary. Do not document or ship AOT as production until
blockers below are resolved and gateway + `--stdio` paths are smoke-tested on
the native exe.

This does **not** reverse [0018](0018-host-identity-and-out-of-process-infrastructure.md):
Native AOT was rejected as a **Runner** design driver. Daemon AOT is an
independent product choice for the tray/MCP host only.

### 4. AOT blockers (inventory — fix or redesign before production AOT)

Grouped by what the 2026-09-03 spike and current graph show. STJ facade rules
and remaining JSON work: [0031](0031-daemon-json-source-gen.md) only — not
duplicated here.

| Area | Location | Why it blocks closed AOT |
|------|----------|---------------------------|
| **ACadSharp** 3.7.1 | `DevTools.FileMetadata.Acad` → Daemon closure (`read_file_info` for `.dwg`) | Third-party assembly; AOT publish logged **IL2104** trim warnings. `AcadFileMetadataReader` uses `ACadSharp.IO.DwgReader`. |
| **OpenMcdf + XmlSerializer** | `DevTools.FileMetadata.Revit` (`TransmissionDataReader`) → Daemon closure | Dynamic XML codegen (**IL2026** / **IL3050**) for Revit transmission metadata. |
| **Collectible ALC + catalog load** | `DevTools.AssemblyIsolation`, `DevTools.Mcp.Catalog` | Dynamic assembly load by design ([0019](0019-ilrepack-and-polyfill-isolated-alc.md), [0027](0027-mcp-product-surface.md)); incompatible with a closed AOT graph while catalog stays in-process. |
| **MCP SDK private reflection** | `DevTools.Mcp.Client/McpClientPassthrough.cs` | Reflection into `McpClientImpl._sessionHandler` for per-call tools/call without MRTR auto-retry ([0027](0027-mcp-product-surface.md)). |
| **STJ / wire JSON** | Daemon `ControlJsonContext`, `UserSettingsJsonContext`; MCP tiers in [0031](0031-daemon-json-source-gen.md) | Partially landed for Daemon-owned types; remaining reflection (`object?`) is 0031 follow-up. |

**Product fork (deferred):** a slim **gateway-only** AOT daemon without catalog /
FileMetadata in the closure is not chosen here; full Daemon keeps hosting catalog
and `read_file_info` until blockers are addressed in place or via an explicit
future ADR.

## Alternatives Considered

1. **Keep WPF/MahApps for Daemon** — preserves familiar XAML tooling but
   retains `PresentationFramework` on the standalone host and blocks Native AOT.
   Rejected for Daemon desktop.
2. **Stay on self-contained ReadyToRun** — smaller deployment without a
   shared runtime, but unacceptable exe size and RAM. Rejected in favor of
   framework-dependent JIT ([`f050d71e`](../../commit/f050d71e4e8c6f66c43c3b378b4a33a1ee8a7224)).
3. **Ship Native AOT after spike compile success** — binary linked, but
   reflection/ALC/catalog paths unverified and warning budget too high. Rolled
   back; JIT remains production.
4. **Drop FileMetadata / catalog from Daemon to greenwash AOT** — would break
   `read_file_info` and in-process toolsets without a product split decision.
   Rejected in the spike; not revisited here.

## Consequences

Positive:

- Daemon desktop no longer depends on WPF/MahApps; Direct2D shell aligns with
  MewUI Native AOT support when blockers clear.
- Framework-dependent JIT publish materially reduces installed footprint and
  memory vs self-contained R2R while keeping a single deployable exe.
- UI, publish posture, and AOT target are one policy agents can cite without
  reading spike plans.

Tradeoffs:

- Machines must have **.NET 10** installed (installer/docs must say so).
- MewUI is a younger stack than WPF — host add-ins and Presentation stay WPF;
  only Daemon moved.
- Native AOT remains aspirational until ACadSharp, ALC/catalog, MCP passthrough,
  and remaining STJ work are resolved or explicitly split.

## Follow-Up

- Track **ACadSharp** AOT/trim story or replace `.dwg` parsing path if the
  package stays incompatible.
- MCP: upstream API for passthrough send or replace `McpClientPassthrough`
  ([0027](0027-mcp-product-surface.md)).
- Catalog / ALC: AOT-compatible story for in-process toolsets or an explicit
  product split ADR.
- STJ: remaining `object?` on invoke/batch DTOs —
  [0031](0031-daemon-json-source-gen.md). Facade collapse:
  [`2026-09-03-stj-facade-0028`](../plans/completed/2026-09-03-stj-facade-0028.md).
- Re-run Native AOT publish + **runtime** smoke (desktop, `--stdio`, gateway,
  control pipe, one `read_file_info` on `.dwg` and `.rvt`) before flipping
  `PublishAot` on the default publish path.

## References

- MewUI shell: [`8c7f91d9`](../../commit/8c7f91d9d630de0f215088b0357f6ab7b7dc1eaf),
  deps [`9bcbf3b6`](../../commit/9bcbf3b65d6b7ed2411f081d3a7fb39c1c54356d).
- JIT publish: [`f050d71e`](../../commit/f050d71e4e8c6f66c43c3b378b4a33a1ee8a7224).
- AOT spike evidence (rolled back): [`docs/plans/completed/2026-09-03-daemon-aot-spike.md`](../plans/completed/2026-09-03-daemon-aot-spike.md).
