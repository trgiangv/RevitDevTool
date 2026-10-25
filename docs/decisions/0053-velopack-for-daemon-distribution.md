# 0053 Velopack Owns Daemon Install and Self-Update; Year Folders Stay Custom

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Part of the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).
Changes 0047 (installer), 0048 (feed), and 0052 (self-update) to build on Velopack.

## Context

Off-the-shelf options were compared (NetSparkle, AutoUpdater.NET, Onova,
Squirrel.Windows, MSIX/ClickOnce, WinGet). None can replace one Autodesk year
folder while a host of another year runs. Velopack (MIT, 1.2.161 stable on
2026-09-29, active repo) covers the rest: per-user install without admin,
`Setup.exe`, delta packages, channels, hash checks, shortcuts, uninstall, and
self-update. The rule is to reuse it and write only what is specific to
Autodesk bundles.

Read from Velopack source (`velopack/velopack`, `develop`, 2026-10-25):

| Behavior | Source | Consequence here |
|----------|--------|------------------|
| `install` into a non-empty folder renames it, then `ensure_empty_dir` | `src/bins/src/commands/install.rs` | Velopack cannot be installed into `RevitDevTool.bundle`; a repair would delete `Contents\<year>\` |
| `apply` swaps `current\` by rename and restores it if the second rename fails; it then refreshes `Update.exe`, stubs, shortcuts | `commands/apply_windows_impl.rs` | Nothing outside `current\` is touched on update |
| `force_stop_package` ends processes whose **image path** is under the root (`QueryFullProcessImageNameW`) | `shared/util_windows.rs` | `Revit.exe` / `acad.exe` are never ended. Daemon `--stdio` processes running from `current\` are |
| The stub starts `Update.exe start` with `CREATE_NO_WINDOW` and exits | `src/bins/src/stub.rs` | The stub does not pass stdio through; MCP clients cannot use it |
| `--framework` prerequisites run the Microsoft installer exe (`/passive`) | `windows/runtimes.rs` | Machine-wide runtime install; not the no-admin path |
| `GithubSource` calls `api.github.com/repos/.../releases` | `lib-csharp/Sources/GithubSource.cs` | Rate-limited (60/h per IP) |
| `IUpdateSource` has two methods: `GetReleaseFeed`, `DownloadReleaseEntry`; `SimpleWebSource` reads `{base}/releases.{channel}.json` | `lib-csharp/Sources/` | A custom source can poll without the REST API and check signatures |
| `VelopackApp`: `SetAppUserModelId`, `OnFirstRun`, `OnRestarted`, `OnAfterUpdateFastCallback`, `OnBeforeUninstallFastCallback`; `UpdateOptions.AllowVersionDowngrade`, `ExplicitChannel` | `lib-csharp/VelopackApp.cs`, `UpdateOptions.cs` | Hooks for first-run selection, uninstall cleanup, toasts, and rollback |

## Decision

### Two install roots

| Root | Owner | Content |
|------|-------|---------|
| `%LOCALAPPDATA%\RevitDevTool\` | Velopack | `Update.exe`, stub `DevTools.Daemon.exe`, `current\` (Daemon, update engine, TestRunner), `packages\` |
| `%APPDATA%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\` | `DevTools.Update` (0049) | `PackageContents.xml`, `Contents\<year>\`, `.updates\`, `install-state.json` |

- The bundle no longer holds the Daemon or TestRunner.
- `AppUtils.GetDaemonExePath` / `GetTestRunnerExePath` point to
  `%LOCALAPPDATA%\RevitDevTool\current\`. That path is stable across updates,
  because Velopack swaps the folder by rename.

### What Velopack does

- Packaging: `vpk pack` over a **self-contained** Daemon publish (folder, not
  single-file). The runtime ships inside the package; deltas keep later updates
  small. No `--framework`, no runtime installer, no admin. This replaces the
  private runtime of 0047.
- Install and uninstall: Velopack `Setup.exe` replaces Inno Setup. CI no longer
  needs ISCC; `install/` and its Pascal includes are removed once this lands.
- Self-update of the Daemon, the engine inside it, and TestRunner: Velopack
  download, delta, apply, restart (0052).
- Shortcuts and AUMID: `VelopackApp.SetAppUserModelId` and the pack AUMID use
  the same id the toasts use (0050).

### What we write

| Piece | Velopack extension point |
|-------|--------------------------|
| Feed without REST API, signature check (0048, 0051) | `IUpdateSource` derived from `SimpleWebSource`, base `https://github.com/<repo>/releases/latest/download/` |
| Schedule (60 min, jitter, backoff) | Daemon hosted service calling `UpdateManager.CheckForUpdatesAsync` |
| Apply gate: no apply while `--stdio` processes run, unless the user agrees | Before `ApplyUpdatesAndRestart` |
| Host × year picker on first run (0047) | `OnFirstRun` |
| HKCU Run value to the stub | `OnFirstRun`, `OnAfterUpdateFastCallback` |
| Remove the bundle and the Run value on uninstall | `OnBeforeUninstallFastCallback` (30 s limit; removes `PackageContents.xml` first so hosts stop loading, then the rest best-effort) |
| Crash-on-start rollback (0052) | `UpdateOptions.AllowVersionDowngrade` with a source pinned to the previous tag |
| Year payloads, lock probe, swap, `PackageContents.xml` (0046, 0049) | None; plain `DevTools.Update` code |

### MCP path migration

- MCP clients must start `%LOCALAPPDATA%\RevitDevTool\current\DevTools.Daemon.exe --stdio`.
  The stub cannot be used.
- For two releases, the old path `RevitDevTool.bundle\Contents\DevTools.Daemon.exe`
  holds a small .NET Framework 4.8 forwarder. It starts the new exe with
  inherited stdio handles and returns its exit code. .NET Framework 4.8 ships
  with Windows 10/11, so it needs no runtime.
- `McpSettingViewModel`, the install guide, and `docs/architecture/MCP/daemon.md`
  show the new path.

## Alternatives Considered

1. **Write everything from scratch.** Full control, but re-implements install,
   uninstall, delta, shortcut, and swap logic that Velopack already tests.
2. **Velopack for everything, years inside the package.** One mechanism, but
   `current\` is replaced as a whole, it cannot live in the bundle, and every
   install carries every year.
3. **Velopack installed into the bundle (`--installto`).** Keeps one folder, but
   install/repair empties the folder and would delete `Contents\<year>\`.
4. **`GithubSource`.** Works out of the box, but uses the rate-limited REST API.
5. **Velopack `--framework net10.0-x64-desktop`.** Small package, but runs the
   machine-wide Microsoft installer.

## Consequences

Positive:

- Inno Setup, Pascal code, ISCC in CI, the private runtime, and most of the
  custom self-update code go away.
- Daemon updates get deltas.
- No admin rights anywhere.

Tradeoffs:

- Two install roots instead of one; uninstall must clean the bundle from a hook
  with a 30 s limit.
- Self-contained Daemon: the first install downloads the runtime inside the package.
- One-time MCP path change for existing users, eased by the forwarder.
- Velopack shows its own apply progress dialog; check how it behaves in silent
  mode before relying on unattended applies.

## Follow-Up

- Spike: `vpk pack` the self-contained Daemon, install, update with a delta,
  check that `current\` keeps its path and that the AUMID toast works.
- Implement the custom `IUpdateSource` and the `--stdio` apply gate.
- Build the net48 forwarder and test it with an MCP client over stdio.
