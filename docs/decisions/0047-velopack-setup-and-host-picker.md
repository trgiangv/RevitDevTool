# 0047 Velopack Setup Plus a First-Run Host Picker Replaces Inno Setup

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).
Built on [0053](0053-velopack-for-daemon-distribution.md). An earlier draft of
this ADR kept Inno Setup and a private runtime; both are dropped.

## Context

`install/Setup.iss` embeds every year of both hosts (177 MB). The component tree
(`revit\2022`…`autocad\2027`) is static and defaults to all checked. `Hooks.inc`
ends running `Revit.exe`, `acad.exe`, `acadlt.exe`, and `DevTools.Daemon.exe`
before files are copied. `XmlFilter.inc` prunes `PackageContents.xml`.
`Daemon.inc` writes the HKCU Run value.

The Daemon is framework-dependent .NET 10 and cannot be Native AOT (0032: MCP
SDK reflection, collectible ALC). The update engine runs in the Daemon (0048),
so the Daemon needs a runtime on every machine without admin rights.

0053 moves Daemon install to Velopack. Velopack `Setup.exe` has no custom
wizard pages, so host and year selection needs another home.

Runtime options checked on 2026-10-25:

| Option | Admin | Finding |
|--------|-------|---------|
| Official runtime installer / Velopack `--framework` | Yes | Machine-wide install |
| `dotnetup` | No | Public preview (2026-10-15); SDK/runtime manager; makes `dotnet` available through the shell profile; Windows Desktop runtime not mentioned. A Daemon started from the Run key does not read the shell profile |
| Runtime zips + `AppHostRelativeDotNet` | No | Works, but the updater must then patch a private runtime |
| Self-contained Daemon inside the Velopack package | No | Runtime ships with the app; Velopack deltas keep updates small |

## Decision

### What the user downloads

`RevitDevTool-Setup.exe` is the Velopack installer. It carries the full
Daemon package (self-contained Daemon with the update engine, TestRunner). It
carries no year payloads.

| Step | Who | Result |
|------|-----|--------|
| Run `Setup.exe` | Velopack | `%LOCALAPPDATA%\RevitDevTool\` with stub, `Update.exe`, `current\`; Start menu shortcut with the toast AUMID |
| First start (`OnFirstRun`) | Daemon | Host picker window; HKCU Run value to the stub |
| User confirms | `DevTools.Update` | Downloads each selected year zip from the manifest of the **same version** as the Daemon, verifies, swaps in (0049), writes `install-state.json` and `PackageContents.xml` |

- The picker lists host × year rows from the manifest `years[].hosts` (0046)
  and preselects rows found on the machine (`IHostPathResolver.GetInstalledVersions`).
- A year is downloaded when at least one of its hosts is selected. Host
  selection only changes `PackageContents.xml`.
- Defaults written to `install-state.json`: `mode: auto`, `channel: stable`,
  `intervalMinutes: 60`.
- The picker is reachable later from the tray (add or remove host years).

### Running hosts at install

Nothing is ended. A selected year that is busy is installed as pending and
applies when its last user exits (0049). The picker says which years wait.

### Offline and silent

- Velopack `Setup.exe --silent` installs the Daemon.
- `DevTools.Daemon.exe --select revit:2024,autocad:2026 [--source <bundle.zip|folder>]`
  runs the year step without UI. `--source` installs years from the full offline
  bundle zip, still checked against the manifest hashes.

### Install state

`RevitDevTool.bundle\install-state.json` is the single record of selection and
per-year versions:

```json
{ "schema": 1, "channel": "stable", "mode": "auto", "intervalMinutes": 60,
  "hosts": [ { "host": "revit", "year": 2024 }, { "host": "autocad", "year": 2026 } ],
  "years": [ { "year": 2024, "version": "3.1.0" }, { "year": 2026, "version": "3.1.0" } ] }
```

`PackageContents.xml` is generated from `hosts`.

## Alternatives Considered

1. **Keep Inno Setup as the router.** Has wizard pages and Pascal logic that
   works, but duplicates install/uninstall with Velopack and needs ISCC in CI.
2. **Private runtime from Microsoft zips (`AppHostRelativeDotNet`).** No admin,
   but the engine has to patch the runtime itself; self-contained in Velopack
   gets the same result with less code.
3. **`dotnetup`.** Preview, WPF runtime support not documented, and the apphost
   still cannot find a shell-profile runtime when started from the Run key.
4. **Machine-wide runtime prerequisite.** Standard, but one UAC prompt per
   machine and blocked on locked-down PCs.

## Consequences

Positive:

- One installer technology (Velopack) for install, update, and uninstall.
- No admin rights and no runtime prerequisite.
- Host selection is WPF code in the Daemon, testable and reusable from the tray.

Tradeoffs:

- Year selection happens after `Setup.exe` closes, in the Daemon window.
- `Setup.exe` is larger than an Inno router because it carries the
  self-contained Daemon.
- Inno-specific behavior (license page, ending processes) is gone.

## Follow-Up

- Daemon first-run picker and `--select` / `--source` CLI.
- Remove `install/`, `CreateInstallerModule`, and ISCC from CI after the spike in 0053.
- Update `docs/docs/getting-started/installation.md` (no prerequisite, offline install, new MCP path).
