# 0049 Lock-Aware Per-Year Apply, Mixed Versions, and Rollback

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).

## Context

Revit loads `Contents\<year>\RevitDevTool.dll` from the `.addin` path, and
AutoCAD loads `AcadDevTool.dll` from the same folder. Those files stay locked
until the host exits. Revit 2024 and AutoCAD 2024 share `Contents\2024\`. Shared
`DevTools.*` dependencies loaded through `AssemblyStreamLoader.LoadFromStream`
do not lock their files, but the entry assemblies always do.

The required behavior: while Revit 2024 runs, other years update; 2024 updates
after it closes. AutoCAD 2024 waits too, even when it is closed, because it
shares the folder. A future Inventor host joins the same folder. The installer
today solves locks by ending processes (`TerminateAllMonitoredProcesses`),
which an unattended updater must not do.

The Daemon and TestRunner no longer live in the bundle; Velopack owns them
under `%LOCALAPPDATA%\RevitDevTool\` (0053). This ADR covers only year folders.

`%APPDATA%` can be redirected to a network share, so `%LOCALAPPDATA%` and the
bundle may be on different volumes.

## Decision

### Lock by year, not by host

- The lock unit is the year folder. If any process holds any file in
  `Contents\<year>\`, the whole year is **busy**, for every host of that year.
  The engine has no per-host lock rules and no list of host DLL names, so a new
  host needs no updater change.
- The probe registers the top-level `*.dll` files of the year folder with
  Restart Manager (`RmStartSession`, `RmRegisterResources`, `RmGetList`). Any
  returned process makes the year busy.
- The same probe gives the PIDs and app names for toasts and the host picker (0047).
- The folder swap below is the second check: a rename that fails means busy.

### Stage then swap, per year

1. Download and verify into `RevitDevTool.bundle\.updates\<ver>\` (same volume
   as `Contents\`, so moves are renames).
2. Extract the year zip into `.updates\<ver>\<year>\`.
3. If the year is not busy: rename `Contents\<year>` to
   `.updates\rollback\<year>-<oldver>`, then rename the staged folder to
   `Contents\<year>`. Update `install-state.json`, then regenerate
   `PackageContents.xml`.
4. If the year is busy: mark it **pending** in `install-state.json` and wait on
   the locker PIDs (`Process.WaitForExitAsync`). Apply when the last one exits.
   Re-probe before the swap.
5. A pending year is also retried at each check and at Daemon start.

Hosts read `PackageContents.xml` and `.addin` at startup, so regenerating them
while another year's host runs is safe.

### Daemon and TestRunner

Velopack updates them (0053); order, apply gate, and rollback are
[0052](0052-updater-self-update.md).

### Mixed versions are legal

- Different years may run different product versions for a while.
- The Daemon must serve hosts of the previous release on the daemon control pipe
  and MCP pipe (0043). `ipcCompat` in the manifest (0046) states the oldest
  generation the new Daemon serves.
- If an installed year is older than `ipcCompat`, the Daemon update waits until
  that year can be updated too; then the Daemon applies first and the year right
  after it.

### Rollback

- Keep one previous version per year under `.updates\rollback\`. Delete older
  ones after a successful apply.
- If the swap fails half-way, rename back and leave the year on its old version.
- The tray offers **Roll back <year>** for the last update. Rollback is the same
  swap in reverse and marks that version as skipped (0050).
- An update that fails verification or extraction is never partially applied.

## Alternatives Considered

1. **Lock per host.** AutoCAD 2024 could update while Revit 2024 runs only if
   the two hosts had disjoint files, but they share `DevTools.*` assemblies in
   one folder. Splitting the folder per host changes the Autodesk bundle layout
   for a rare gain.
2. **End host processes, like the installer.** Simple, but loses unsaved work; not
   acceptable without the user.
3. **Versioned side-by-side folders (`Contents\2024\3.1.0\`) and rewrite `.addin`.**
   No swap needed, but `.addin` and `PackageContents.xml` then point into deep
   paths, and old folders pile up.
4. **`MoveFileEx(MOVEFILE_DELAY_UNTIL_REBOOT)`.** Needs elevation for most paths
   and waits for a reboot.
5. **Staging in `%LOCALAPPDATA%`.** Breaks atomic rename on redirected profiles.

## Consequences

Positive:

- No host process is ever killed by the updater.
- Each year moves atomically from one version to the next.
- Rollback is a rename, not a re-download.

Tradeoffs:

- Bundle grows by one staged and one rollback copy per changed year (~100 MB each).
- Mixed-version fleets make pipe compatibility a release rule, not an accident.
- Restart Manager behavior on DLLs loaded by Revit/AutoCAD must be proven on a
  live host before acceptance.

## Follow-Up

- Prove `RmGetList` reports Revit and AutoCAD when they have the year folder's
  DLLs loaded (2024 and 2026).
- Add an `ipcCompat` generation constant next to the pipe contracts in `DevTools.Ipc`.
- Tests: swap, pending, rollback, and half-failed swap with a fake file locker.
