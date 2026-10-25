# 0050 Update Modes and Windows Toast Notifications

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).

## Context

Users need to choose between automatic and manual updates and must be told what
happened through Windows toasts. The Daemon tray is HandyControl `NotifyIcon`
(`docs/architecture/MCP/daemon.md`). User preferences live in `UserSettings`
(`%APPDATA%\RevitDevTool\settings.json`).

Windows App SDK `AppNotificationManager` works for unpackaged apps but adds the
Windows App SDK runtime to a single-file Daemon. The inbox WinRT
`ToastNotificationManager` works from desktop apps when the AppUserModelID is
known to the shell, for example through a Start menu shortcut that carries it.
Velopack creates the Start menu shortcut and sets its AUMID; the app sets the
same id with `VelopackApp.SetAppUserModelId` (0053).

## Decision

### Modes

| Mode | Check | Download | Apply |
|------|-------|----------|-------|
| `auto` (default) | Yes | Yes | Free years apply; busy years apply when the last process using them exits; the Daemon applies when no `--stdio` process runs (0052) |
| `notify` | Yes | Yes, in background | Only after the user chooses **Update** |
| `off` | Only on demand from the tray | On demand | On demand |

- Default is `auto`. The first-run host picker (0047) shows the mode with a
  **Notify me instead** option, so the choice is visible at install.
- `auto` never interrupts work: a busy year waits, a Daemon update waits for MCP
  sessions to end, and nothing is killed without the user.
- Channel: `stable` (default) or `prerelease`.
- **Skip this version** stores the version; the engine ignores it until a newer one appears.
- Settings live in `install-state.json` (mode, channel, interval, skipped
  version) next to the year state; the picker and the Daemon settings view edit
  the same file. Velopack's channel follows `channel` (`UpdateOptions.ExplicitChannel`).
- The updater never ends host processes, in any mode.

### Notifications

Toasts are short and grouped per release, not per year:

| Event | Toast | Actions |
|-------|-------|---------|
| Update available (`notify`) | "RevitDevTool 3.1.0 is ready" + years affected | **Update**, **Release notes**, **Skip** |
| Applied | "Updated 2025, 2026 to 3.1.0" | **Release notes** |
| Pending | "2024 (Revit, AutoCAD) updates when Revit 2024 closes" | — |
| Daemon pending | "Restart Claude, Cursor to finish updating RevitDevTool" (MCP clients holding `--stdio`) | **Update now** after 24 h |
| New host year found | "Revit 2027 detected. Add RevitDevTool?" | **Add**, **Dismiss** |
| Failed | "Update to 3.1.0 failed; still on 3.0.0" | **Open log** |

- Use inbox `ToastNotificationManager` with a fixed AUMID. The Velopack pack
  sets it on the shortcut, and `VelopackApp.SetAppUserModelId` sets it for the
  process.
- Toast activation arguments are routed through the existing single-instance
  path: a second Daemon launch forwards the arguments on the daemon control pipe
  instead of exiting silently.
- If toasts are disabled by policy or Focus Assist, fall back to a tray balloon
  and a badge in the Daemon window. Nothing blocks on a toast.
- Hosts show no update UI. At most, the host panel shows its own product version.

### Telemetry

Update operations are `devtool.update` transactions under 0045 with `kind` =
`check`, `download`, `apply`, `rollback`. No paths or version lists beyond the
from/to product versions.

## Alternatives Considered

1. **Default to `notify`.** Every update needs a click, so most machines stay on
   old versions. `auto` is safe enough because it never touches a year in use.
2. **Windows App SDK notifications.** Richer API, but a new runtime dependency
   for one feature.
3. **Tray balloons only.** No AUMID work, but balloons are legacy and less visible.
4. **Notify from inside the host.** Visible where the user works, but the host
   cannot update its own year, and every open host would repeat the message.

## Consequences

Positive:

- One consent model, one place to change it, one notification source.
- No new runtime for toasts.

Tradeoffs:

- Toast actions depend on the shortcut AUMID; a portable Velopack install only
  gets balloons.
- The single-instance guard changes from "exit silently" to "forward arguments".

## Follow-Up

- Set the AUMID in `vpk pack` and `VelopackApp.SetAppUserModelId`; prove a toast
  action reaches the tray.
- Forward second-instance arguments on the daemon control pipe (0043 method list).
- Daemon settings view: mode, channel, interval, skipped version, roll back.
