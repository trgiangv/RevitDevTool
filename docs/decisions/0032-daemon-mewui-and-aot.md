# 0032 Daemon Desktop Is HandyControl WPF; Native AOT Is Dropped

Date: 2026-09-04
Amended: 2026-09-27

## Status

Accepted. **Amended 2026-09-27.** The MewUI shell and the Native AOT target are
withdrawn. Daemon desktop is HandyControl WPF. Production publish stays
framework-dependent JIT single-file. Do not reintroduce MewUI, `PublishAot`,
or a Direct2D backend.

Companion to [0018](0018-host-identity-and-out-of-process-infrastructure.md)
(Runner AOT stays rejected),
[0027](0027-mcp-product-surface.md) (MCP product surface on this process),
[0031](0031-daemon-json-source-gen.md) (source-gen JSON on Daemon wires; no
longer an AOT gate),
and [0037](0037-handycontrol-replaces-mahapps.md) (HandyControl lives in
`DevTools.UI`).

Living map: [`docs/architecture/MCP/daemon.md`](../architecture/MCP/daemon.md).

## Context

`DevTools.Daemon` is the standalone external MCP host: tray desktop, gateway
tunnel, control pipe, and `--stdio` MCP server. It used WPF, then moved to
MewUI Direct2D so the process could leave `PresentationFramework` and aim at
Native AOT. A 2026-09-03 AOT spike linked a native binary and was rolled back.
The AOT blockers (ACadSharp, collectible ALC, MCP SDK reflection) are product
behavior, not a UI problem.

Host add-ins now compile HandyControl into `DevTools.UI`
([0037](0037-handycontrol-replaces-mahapps.md)). A second UI stack on the tray
process is not worth an AOT goal this product is not shipping.

Publish stays **framework-dependent JIT** single-file. That cut the
self-contained ReadyToRun exe and RAM footprint and still ships one
`DevTools.Daemon.exe` that requires an installed **.NET 10** runtime.

## Decision

### 1. Daemon desktop UI is HandyControl WPF — **Accepted**

- `UseWPF` on `DevTools.Daemon`. Views are XAML under
  `source/DevTools.Daemon/Views/` (`MainWindow` is a standard
  `Window`. Title-bar theme and system buttons go through Win32, the same
  path as `StubBuilderWindow`).
- Theme is the HandyControl skin and `Themes/Theme.xaml` compiled into
  `DevTools.UI`, merged only in Daemon `Application.Resources`.
  `ThemeHelper` reloads those two dictionaries the same way as the
  HandyControl demo. It does not call `ThemeManager`, so add-in views that
  merge `Theme/Theme.xaml` per control keep their own skin swap.
  `AppTheme.Auto` follows the Windows app theme.
- **Tray** — HandyControl `NotifyIcon` (`hc:NotifyIcon`). The icon is created
  in code and `Init()` is called directly, because the dashboard starts hidden
  and `Loaded` would never run. Right-click uses the WPF `ContextMenu` in
  `TrayResources.xaml`. Close on the main window hides it; Quit shuts the
  process down.
- MewUI (`Aprillz.MewUI.Windows`, `MewUIBackend`, C# markup views) is removed.
  Do not add it back.

This process is its own WPF application. Merging the theme at
`Application` scope does not leak into Revit or AutoCAD.
[0037](0037-handycontrol-replaces-mahapps.md) still forbids that merge inside
host add-ins.

### 2. Production publish stays framework-dependent JIT — **Accepted**

Default `dotnet publish -c Release` (and `PublishDaemonModule`) uses:

| Flag | Value |
|------|--------|
| `SelfContained` | `false` |
| `PublishSingleFile` | `true` |
| `PublishReadyToRun` | not set |
| `PublishAot` | not set, and not a target |

Output is a **single-file exe that requires the .NET 10 runtime**.

### 3. Native AOT is dropped for Daemon — **Accepted**

Do not add `PublishAot`, a trim publish profile, or an AOT spike follow-up
for this process. [0018](0018-host-identity-and-out-of-process-infrastructure.md)
already rejected Native AOT as a Runner design driver. Daemon no longer
carries a separate AOT goal.

Source-generated JSON on Daemon wires stays for the reasons in
[0031](0031-daemon-json-source-gen.md). It is not an AOT milestone.

## Alternatives Considered

1. **Keep MewUI so a future AOT publish stays possible.** Rejected. AOT is
   not the product. The blockers are catalog, file metadata, and the MCP SDK,
   and the hosts already standardized on HandyControl.
2. **WPF Fluent `ThemeMode` without HandyControl.** Rejected. The rest of the
   desktop UI is HandyControl in `DevTools.UI`.
3. **Stay on self-contained ReadyToRun.** Rejected. Exe size and RAM. See
   [`f050d71e`](../../commit/f050d71e4e8c6f66c43c3b378b4a33a1ee8a7224).
4. **Ship the 2026-09-03 Native AOT binary.** Rejected then, and withdrawn
   as a target here.

## Consequences

Positive:

- One UI stack with the host add-ins: HandyControl compiled into `DevTools.UI`.
  Daemon theme reload stays on its `Application.Resources` and does not walk
  add-in control dictionaries.
- Framework-dependent JIT publish stays the small single-file deploy.
- Agents stop treating MewUI or `PublishAot` as Daemon policy.

Tradeoffs:

- Machines must have **.NET 10** installed.
- Daemon references `DevTools.UI` and `PresentationFramework`. It is not a
  candidate for a closed Native AOT graph.
- Host add-ins still must not merge this theme into `Application.Resources`.
  Daemon may, because it is the application.

## References

- MewUI shell, now removed: [`8c7f91d9`](../../commit/8c7f91d9d630de0f215088b0357f6ab7b7dc1eaf).
- JIT publish: [`f050d71e`](../../commit/f050d71e4e8c6f66c43c3b378b4a33a1ee8a7224).
- AOT spike evidence: [`docs/plans/completed/2026-09-03-daemon-aot-spike.md`](../plans/completed/2026-09-03-daemon-aot-spike.md).
