# 0046 Per-Year Release Payloads and Update Manifest

Date: 2026-10-25

## Status

**Proposed 2026-10-25.**

First of the auto-update set: 0046 (release artifacts), [0047](0047-velopack-setup-and-host-picker.md)
(installer and host picker), [0048](0048-daemon-hosted-update-engine.md) (engine and discovery),
[0049](0049-lock-aware-per-year-apply.md) (year apply and rollback),
[0050](0050-update-policy-and-toast-notifications.md) (policy and toasts),
[0051](0051-update-integrity-and-trust.md) (integrity),
[0052](0052-updater-self-update.md) (updater self-update),
[0053](0053-velopack-for-daemon-distribution.md) (Velopack for the Daemon).

## Context

Today one release publishes two assets (release `3.0.0`, checked with `gh release view`):

| Asset | Size | Content |
|-------|------|---------|
| `RevitDevTool-Setup.exe` | 177 MB | Inno Setup with every year embedded |
| `RevitDevTool.bundle.zip` | 301 MB | Full `.bundle`, every host and year |

`CreateBundleModule` copies Revit and AutoCAD publish output for the same year
into one `Contents\<year>\` folder. A future Inventor host would land in the same
folder. `DevTools.Daemon.exe` and `DevTools.TestRunner.exe` sit in `Contents\`.
One year folder is about 100 MB uncompressed (2024–2027 measured on a dev
machine: 92–104 MB, 143–149 files).

### Reference: RevitLookup

`lookup-foundation/RevitLookup` release `2027.0.4` publishes one MSI per Revit
year and install scope (`RevitLookup-2024.3.4-SingleUser.msi`, ~4–5 MB each,
14 MSIs) plus `RevitLookup.bundle.zip` (41 MB). Its `SoftwareUpdateService`
runs inside Revit, reads the REST `releases` list, picks the newest
non-prerelease, and matches an asset by regex on the year-major version and
the `SingleUser`/`MultiUser` tag. The user runs the downloaded MSI.

What carries over: one asset per year, and the full bundle zip for offline use.
What does not: RevitLookup has one host per year folder, so a year MSI is the
whole product for that year. Here one year folder holds several hosts. Asset-name
parsing and REST list calls are what 0048 avoids.

## Decision

### Release assets

| Asset | Content |
|-------|---------|
| `RevitDevTool-<ver>-<year>.zip` | Everything under `Contents\<year>\`: every host built for that year |
| `RevitDevTool-Setup.exe` | Velopack installer for the Daemon package (0047, 0053), no year payload |
| `RevitDevTool-<ver>-full.nupkg`, `RevitDevTool-<ver>-delta.nupkg` | Velopack Daemon packages (self-contained Daemon, update engine, TestRunner) |
| `releases.win.json` (+ `releases.<channel>.json` for prerelease) | Velopack feed |
| `RevitDevTool.bundle.zip` | Full offline year bundle, for air-gapped installs and CI artifacts |
| `update-manifest.json` | Year manifest, uploaded last |
| `*.sig` | Signatures of the Velopack feed and the year manifest (0051) |

- The **year** is the only payload unit, for download, apply, and lock (0049).
  Which hosts of that year are active is decided by `PackageContents.xml`, not by
  which files are on disk.
- A new host (Inventor) adds files to the year zips and entries to the template.
  It adds no asset kind and no manifest field.
- Files that two hosts of the same year both publish (shared `DevTools.*`
  assemblies) must be byte-identical. The pipeline fails the release if a hash
  differs. Today the copy order silently decides which one wins.
- The product keeps one version across years (`3.1.0`). The year is in the
  asset name, not in the version as RevitLookup does it.

### Manifest

The Daemon package is described by the Velopack feed (0053). `update-manifest.json`
describes only the year folders. Clients never parse release notes or asset names.

Terminology (2026-10-25): a **payload** is one `Contents\<host version>\`
folder. The **host version** is a string (`"2024"`), not a year number. An
**add-in target** is one `PackageContents.xml` component: `Platform` plus host version.

```json
{
  "schema": 1,
  "version": "3.1.0",
  "channel": "stable",
  "publishedAt": "2026-11-02T10:00:00Z",
  "notesUrl": "https://github.com/trgiangv/RevitDevTool/releases/tag/3.1.0",
  "minUpdaterVersion": "3.1.0",
  "ipcCompat": 1,
  "payloads": [
    { "hostVersion": "2024", "platforms": ["AutoCAD*", "Revit"], "asset": "RevitDevTool-3.1.0-2024.zip", "sha256": "…", "size": 0 }
  ]
}
```

- `platforms` is the exact `PackageContents.xml` `Platform` value of each
  component that has its module file in that payload. The engine and the
  pipeline share one model (`DevTools.Update.Releases.UpdateManifest`).
- Payload zip entries are relative to `Contents\<host version>\` (no folder prefix).
- A year missing from `years` means the release dropped support. Clients keep
  the installed version of that year and do not delete it.
- `ipcCompat` is the oldest control/MCP pipe contract generation the Daemon of
  this release still serves (0049).
- `minUpdaterVersion` makes years wait until the Daemon has updated itself (0052).
- The `PackageContents.xml` template ships inside the Daemon package, so the
  engine that writes it always matches the template.

### Pipeline (`build/`)

- New `PackPayloadsModule` after `CreateBundleModule`: one zip per year folder
  and the identical-hash check.
- New `GenerateUpdateManifestModule`: writes the year manifest from those zips.
- New `PackDaemonModule`: `dotnet publish` the Daemon self-contained (folder),
  `vpk download github` the previous release for deltas, then `vpk pack`.
  `vpk` is a pinned .NET tool in the repo tool manifest.
- `CreateInstallerModule` (Inno, ISCC) is removed once 0053 lands.
- `PublishGithubModule` creates the release as a **draft**, uploads year zips,
  Velopack packages, and signatures, uploads the Velopack feed and
  `update-manifest.json` last, then publishes the draft. `/releases/latest`
  never points to a release with missing assets.
- `Compile.yml` uploads the full bundle zip and the Velopack `Setup.exe` as PR
  artifacts.

## Alternatives Considered

1. **Keep one full bundle and diff on the client.** Smallest CI change, but every
   update downloads ~300 MB and the installer has nothing to select.
2. **One payload per host × year.** A Revit-only user skips AutoCAD bits, but two
   zips write into one folder, the apply unit no longer matches the download
   unit, and each new host multiplies the asset count.
3. **One installer per year, RevitLookup style.** Proven, but it puts the selection
   logic in N installers and still needs an updater.
4. **Year folders as Velopack packages.** Deltas for years too, but Velopack
   needs an exe per package, replaces its whole `current\`, and empties a
   non-empty install root (0053). Velopack deltas apply to the Daemon package only.
5. **Derive metadata from the GitHub Releases API.** Costs rate-limited calls and
   ties clients to asset naming.

## Consequences

Positive:

- Install and updates download only selected years; one zip per year regardless
  of host count.
- The Daemon gets Velopack deltas; years get a small JSON contract that also
  covers future hosts.
- Draft-then-publish removes the partial-release window.

Tradeoffs:

- A user with only Revit 2024 still downloads the AutoCAD 2024 files (roughly
  double the year zip).
- About 14 assets per release instead of 2, and two feeds (Velopack and years).
- The year manifest schema becomes a compatibility surface; `schema` must be
  bumped, not silently changed.

## Follow-Up

- Implement `PackPayloadsModule`, `GenerateUpdateManifestModule`, `PackDaemonModule`.
- Change `PublishGithubModule` to draft, upload, feeds last, publish.
- Document the manifest schema in `docs/architecture/` once it lands.
