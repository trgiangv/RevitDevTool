# Execution Plan: Auto-Update Engine (Velopack Daemon + Custom Year Folders)

Date: 2026-10-25

## Status

Active — Phase 1 (build pipeline) implemented and proven locally. The Inno
installer is removed and `pack` is the only release flow (Phase 3 steps 3–4
pulled forward on 2026-10-25). `Compile.yml` and the draft-then-publish path
are not yet exercised in CI. **Do not tag a release before Phase 2**: the new
`Setup.exe` installs the Daemon without year folders. Phase 2 next.

## Outcome

A user installs RevitDevTool with one Velopack `Setup.exe`, picks host × year
on first run, and from then on the Daemon keeps itself and every selected year
folder current, hourly, without admin rights and without ending any host
process. Releases are produced by `build/` and published by
`PublishRelease.yml`.

## Context

- Decisions: [0046](../../decisions/0046-per-year-release-payloads-and-update-manifest.md)
  (release assets, year manifest), [0047](../../decisions/0047-velopack-setup-and-host-picker.md)
  (Setup + host picker), [0048](../../decisions/0048-daemon-hosted-update-engine.md)
  (engine, polling), [0049](../../decisions/0049-lock-aware-per-year-apply.md)
  (per-year lock and swap), [0050](../../decisions/0050-update-policy-and-toast-notifications.md)
  (modes, toasts), [0051](../../decisions/0051-update-integrity-and-trust.md)
  (signing), [0052](../../decisions/0052-updater-self-update.md) (self-update),
  [0053](../../decisions/0053-velopack-for-daemon-distribution.md) (Velopack split).
- Build pipeline: `build/Program.cs`, `build/Modules/*`, `build/appsettings.json`.
- CI: `.github/workflows/Compile.yml`, `PublishRelease.yml`.
- Daemon entry: `source/DevTools.Daemon/Program.cs`, `DevTools.Daemon.csproj`.
- Inno installer (`install/`, `CreateInstallerModule`): removed 2026-10-25.

## Scope

In scope:

- Phase 1: build modules for year zips, year manifest, Velopack Daemon package,
  feed signing, draft-then-publish; wired into the `pack` pipeline argument.
- Phase 2: `DevTools.Update` engine (feed source, scheduler, lock probe, swap,
  rollback) and Daemon integration (first-run picker, hooks, toasts, start guard).
- Phase 3: cutover — MCP path forwarder, path changes, install docs.

Out of scope:

- Authenticode certificate purchase (0051 keeps it optional).
- WinGet publishing.
- MSI / per-machine installs.

## Approach

### Phase 1 — Build pipeline (this session)

`dotnet run --project build -- pack`. Every asset lands flat in `output/`:

```text
CreateBundleModule ─► PackPayloadsModule ─► GenerateUpdateManifestModule ─┐
CompileDaemonModule ─► PackDaemonModule ──────────────────────────────────┼─► SignUpdateFeedsModule
                                                                          └─► PublishGithubModule (with publish)
```

| Module | Output |
|--------|--------|
| `CreateBundleModule` (changed) | Only `bin/Release.*/publish`; fails when a file two hosts of one year publish differs; clears the stale bundle in `output/` |
| `PackPayloadsModule` | `output/RevitDevTool-<ver>-<year>.zip`, entries relative to `Contents\<year>`; hosts per year from `PackageContents.xml` components whose module file exists |
| `GenerateUpdateManifestModule` | `output/update-manifest.json` (schema 1) |
| `PackDaemonModule` | Clears last run's Velopack files in `output/`; self-contained Daemon publish (`VelopackPublish=true`) to `source/DevTools.Daemon/bin/Release/velopack`; `vpk download github` (non-fatal); `vpk pack --noPortable` to `output/`; drops `assets.*.json`; renames `RevitDevTool-<channel>-Setup.exe` to `RevitDevTool-Setup.exe` |
| `SignUpdateFeedsModule` | `<feed>.sig` (ECDSA P-256 / SHA-256, base64 IEEE P1363) for the manifest and `releases.*.json`; skipped without `Update__SigningKey`, fails when `Update__RequireSignature` is true |
| `PublishGithubModule` (changed) | Draft release → payloads in parallel → feeds and signatures last → publish; top-level `output/` files, minus nupkgs of other versions |

`vpk` 1.2.161 is pinned in the root `dotnet-tools.json`. The Daemon references
`Velopack` 1.2.161 and calls `VelopackApp.Build().SetAutoApplyOnStartup(false).Run()`
in tray mode only (`--stdio` keeps stdout clean).

`Compile.yml` and `PublishRelease.yml` run `pack`. `PublishRelease.yml` sets
`Update__SigningKey` from the `UPDATE_SIGNING_KEY` secret and
`Update__RequireSignature: true`, so a release without the secret fails.

### Phase 2 — Engine and Daemon

`source/DevTools.Update` (net10.0-windows, headless) + `tests/DevTools.Update.Tests`:

| Namespace | Types | Role |
|-----------|-------|------|
| `Autodesk` | `AutodeskBundle`, `AddinTarget`, `AddinComponent`, `PackageContents` | Bundle paths. A target is one `PackageContents.xml` component (`Platform` + host version). `PackageContents` removes unselected components from the template and keeps all other data |
| `Installations` | `Installation`, `UpdatePolicy`, `InstalledPayload`, `InstallationStore` | `installation.json`: selection, installed payloads, policy |
| `Releases` | `IReleaseProvider`, `GitHubReleaseProvider` (Octokit), `Release`, `ReleaseReader`, `ReleaseSignature`, `UpdateManifest`, `VelopackReleaseSource` | Provider-neutral release access. A private deploy server is one more `IReleaseProvider`. Velopack stays the installer engine through `VelopackReleaseSource` |
| `Apply` | `IFolderLock`, `RestartManagerFolderLock`, `PayloadInstaller` | Lock check, stage, swap, offline bundle |
| `Engine` | `UpdateEngine`, `UpdateScheduler`, `DaemonUpdater`, `IUpdateNotifier` | Cycle, schedule, Velopack self-update |

Daemon: Updates tab in the main window (loading line, "No update is available"),
tray balloon notices, Velopack install/uninstall hooks (Run key, bundle removal),
`--select "Revit 2024" --select "AutoCAD 2026" [--source <bundle.zip>] [--notify]`.
`AppUtils` prefers `%LOCALAPPDATA%\RevitDevTool` for the Daemon and TestRunner.
The TestRunner is in the same Velopack folder and uses the Daemon's .NET runtime
(`VelopackPublish` gives it the WPF runtime profile). `PackDaemonModule` fails if the
two apps publish a file with the same name and different bytes. 2026-10-25: 405 shared
files identical, 11 TestRunner-only files; full nupkg 74.6 MB, `Setup.exe` 81.7 MB.

Not done in Phase 2: start guard + downgrade (0052), Windows toasts with AUMID
(balloons for now), Skip and Rollback in the UI (the engine had them with no
caller, so they were removed).

### Phase 3 — Cutover

1. Dropped 2026-10-26: no stdio forwarder. The 3.0.0 migration deletes the old executables; the 4.0.0 notes tell users to copy the new MCP snippet (0054).
2. Done 2026-10-25: `AppUtils` finds the Daemon and the TestRunner in `%LOCALAPPDATA%\RevitDevTool\current`,
   else in the bundle `Contents` (dev deploy). The MCP snippet in Settings and the TestAdapter use it; no path is hard-coded.
   The bundle zip has only payloads and `PackageContents.xml`. Open: user install guide (`docs/docs/getting-started/installation.md`)
   at the cutover, because release 3.0.0 is still the Inno installer.
3. Done early: `PublishRelease.yml` signs with `UPDATE_SIGNING_KEY`, signing mandatory.
4. Done early: `install/`, `CreateInstallerModule`, ISCC removed; `pack` builds the new assets.

### Real release proof ([0054](../../decisions/0054-release-automation-channels-and-4-0-cutover.md))

1. Done 2026-10-26: default channel from the Velopack install, **Include pre-releases**, 3.0.0 migration,
   `VerifyReleaseModule`, CI gate and `environment: release`, `TestReleaseInstall.yml`, SHA pins, Dependabot,
   `next-version: 4.0.0`. Proof: `DevTools.Update.Tests` 25/25, `DevTools.Daemon.Tests` 106/106,
   `Build__Version=4.0.0-beta.1` `pack` with `Update__RequireSignature=true` and a temporary public key: 12 modules
   passed, `VerifyReleaseModule` passed.
   Owner: run `scripts/new-update-signing-key.ps1`, commit the public key, add a reviewer to the `release` environment.
2. `4.0.0-beta.1`: clean install on Windows Sandbox (Setup, Updates tab, Revit and AutoCAD load,
   MCP snippet path, TestRunner from the TestAdapter).
3. `4.0.0-beta.2`: Daemon self-update (delta), payload update with a host open (pending, then applied),
   notify mode.
4. 3.0.0 (Inno) → 4.0.0-beta migration on a clean VM.
5. `4.0.0` stable.

## Risks And Recovery

- **Releases lost the Inno installer before Phase 2 lands.** A tag now ships a
  `Setup.exe` that installs only the Daemon; the Run key, process shutdown and
  `PackageContents.xml` pruning Inno did are gone until Phase 2. Recovery: do
  not tag; users who need add-ins extract `RevitDevTool.bundle.zip` by hand.
- **Missing `UPDATE_SIGNING_KEY` fails the release.** Recovery: add the secret
  (PEM P-256 private key) before tagging.
- **Draft-then-publish is new release code** and is first exercised on the next
  tag. Recovery: if `Edit(Draft=false)` fails, publish the draft by hand; the
  tag is already pushed and `OnFailedAsync` deletes it only when the module throws.
- **Hash check can fail a release** if two hosts of a year ever publish different
  bytes for one file. 2026-10-25 check: 0 differing of 138–144 shared files per
  year (2022–2027). Recovery: fix the project producing the divergent file.
- **Velopack `Setup.exe` in CI artifacts installs a Daemon without year
  folders** until Phase 2. It is not attached to releases before Phase 3.
- **Daemon `obj` race:** a parallel `dotnet build` of the Daemon hit
  `CS2012` on `DevTools.FileMetadata.Core` (referenced twice through
  `SetTargetFramework`); `-m:1` passed. Pre-existing; not caused by this plan.

## Progress

- [x] ADRs 0046–0053 proposed.
- [x] `UpdateOptions`, `appsettings.json` `Update` section.
- [x] `PackPayloadsModule`, `GenerateUpdateManifestModule`, `PackDaemonModule`, `SignUpdateFeedsModule`.
- [x] `CreateBundleModule` Release-only filter, stale bundle cleanup, identical-hash check.
- [x] `PublishGithubModule` draft → payloads → feeds → publish.
- [x] `Program.cs` wiring behind `pack`; Inno flow removed; assets flat in `output/`.
- [x] Daemon: `Velopack` reference, `VelopackPublish` switch, `VelopackApp` in tray `Main`.
- [x] `vpk` pinned in `dotnet-tools.json`.
- [x] End-to-end `dotnet run -- update` on a dev machine (2026-10-25, `Build__Version=3.0.1`):
  11 modules passed in 5 m 39 s. Six year zips (51.9–59.4 MB, hosts `autocad, revit`),
  manifest hash matches the zip, `vpk` verified `VelopackApp.Run()`, Velopack
  `Setup.exe` 80.3 MB / full nupkg 73.1 MB, no delta (no earlier Velopack release).
  Second run with a throwaway P-256 key: both `.sig` files verify, a one-byte change fails.
- [ ] `Compile.yml` green with `pack`.
- [x] Phase 2 engine, Updates tab, tray notices, Velopack hooks, `--select` (2026-10-25).
  `dotnet run --project tests/DevTools.Update.Tests` 21/21.
- [ ] Phase 2 remaining: start guard + downgrade, Windows toasts, Skip/Rollback UI,
  live install test from a real release.
- [ ] Phase 3.

## Decisions

- 2026-10-25: Payload zip entries are relative to `Contents\<host version>` (no prefix); the
  engine extracts into `.updates\<ver>\<host version>\`.
- 2026-10-25: Use "host version" (string), not "year". Platform ids are the exact
  `PackageContents.xml` `Platform` value (`Revit`, `AutoCAD*`). A new host needs only a template entry.
- 2026-10-25: Autodesk.PackageBuilder 2.0.2 was checked and not used: it only writes
  (`ExtensibleData.ReadXml` throws `NotImplementedException`). The engine filters the
  template with `XDocument` and removes whole components, so no template data is lost.
- 2026-10-25: GitHub releases come from Octokit (typed models, `RateLimitExceededException`).
  One REST call per check; 60 calls/h per IP without a token.
- 2026-10-25: `build/` references `DevTools.Update` and writes the manifest with the same
  `UpdateManifest` model and reads hosts with the same `PackageContents` reader.
- 2026-10-25: Signature format is base64 IEEE P1363, the default of
  `ECDsa.VerifyData`, so the client needs no format argument.
- 2026-10-25: Prerelease versions use Velopack channel `prerelease`; stable uses
  Velopack's default channel.
- 2026-10-25: `vpk` 1.2.161 has no AUMID option; Phase 2 uses the AUMID Velopack
  writes on the Start menu shortcut.
- 2026-10-25: `VelopackApp.Run()` runs only outside `--stdio` and with
  auto-apply off, so pending updates go through the 0052 apply gate.

## Validation

- Focused proof: `dotnet build build/Build.csproj -c Release`;
  `dotnet build source/DevTools.Daemon/DevTools.Daemon.csproj -c Release -m:1`.
- Integration proof: `Build__Version=3.0.1 dotnet run -c Release -- pack` from
  `build/` produces the bundle zip, year zips, `update-manifest.json`,
  `RevitDevTool-Setup.exe`, the full nupkg and `releases.win.json` in `output/`.
- Repository-required checks: `Compile.yml` on the PR.

## Result

Pending.
