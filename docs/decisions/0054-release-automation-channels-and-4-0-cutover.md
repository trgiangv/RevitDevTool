# 0054 Release Automation, Channels, and the 4.0.0 Cutover

Date: 2026-10-25

## Status

**Proposed 2026-10-25.** Builds on the auto-update set
([0046](0046-per-year-release-payloads-and-update-manifest.md)–[0053](0053-velopack-for-daemon-distribution.md)).
It changes how a release is made, not what a release contains.

## Context

### Our pipeline today

| Workflow | Trigger | Does |
|----------|---------|------|
| `Compile.yml` | Every push and PR | `dotnet run -- pack` (5 min), uploads the bundle zip, `Setup.exe`, manifest, Velopack feed |
| `PublishRelease.yml` | Tag push, or dispatch with a version (the workflow creates the tag) | Builds again with `pack publish`, signs the feeds (`UPDATE_SIGNING_KEY`, mandatory), draft → upload → publish, fast-forwards `develop` |
| `PublishTestAdapter.yml` | Manual dispatch | Packs `RevitDevTool.TestAdapter`, pushes to nuget.org with OIDC |
| `Docs.yml` | `docs/**` changes | Builds the Docusaurus site |

Version: GitVersion (`ContinuousDeployment`, `Mainline`) or `Build__Version`.
Tags have no `v` prefix (`3.0.0`). Release notes come from `GenerateChangelogModule`.

Gaps: the release builds again, so the shipped bytes are not the bytes CI
tested. No job installs the release on a clean machine. Nothing checks that
the feed signatures verify with the public key the Daemon ships. Actions are
pinned by major tag, not by SHA. Jobs with secrets have no protected environment.

### Reference: pyRevit (`pyrevitlabs/pyRevit`, ModularPipelines, Windows)

| Pattern | How |
|---------|-----|
| One pipeline, channel from the ref | `Build__Channel` = `wip` (push to `develop`), `release` (tag or `master`), `none` (PR); the same `build/` runs `ci`, `pack sign`, `release pack sign publish` |
| Build once, promote | `release.yml` waits for the `ci.yml` run on the tagged SHA (`gh run watch`), downloads its unsigned artifact, then packs, signs, publishes. No rebuild |
| WIP builds | Each push to `develop` gives signed installers as Actions artifacts and a rolling `ci-binaries` prerelease (per-SHA zip + branch-latest zip). A prune step keeps the last 3 |
| Protected secrets | `environment: production` on jobs that sign; Azure Trusted Signing through `Signing__*` |
| Verify after sign | Fails if any candidate is not Authenticode-valid |
| Real host test | Self-hosted runner with Revit 2022–2027 loads the signed add-in and reads the journal |
| Distribution | `winget.yml` on `release: published`; it verifies the signature of `wingetcreate.exe` before use |
| Hygiene | Actions pinned by SHA, `permissions` per job, path filters, `concurrency`, a `github.repository == main` guard for forks, temp artifacts deleted after promotion |
| Feedback | A `notify` command comments on linked issues after WIP and release builds |

### Reference: TUnit (`thomhurst/TUnit`, ModularPipelines, NuGet)

| Pattern | How |
|---------|-----|
| Version from history | GitVersion 6 `Mainline`; the CI exports `Version`, `AssemblyVersion`, … as env vars so every build in the job stamps the same version |
| Release on demand from `main` | `workflow_dispatch` input `publish-packages`; pipeline modules have `[RunOnlyOnBranch("main")]` |
| Test what ships | Packs, then tests the packed packages (consumer projects, all TFMs, a separate Windows job) before any push |
| Cross-workflow gate | `UploadToNuGetModule` waits until `aot.yml` passed for the same SHA, fails closed on timeout or failure |
| Fail-closed guards | Refuses to publish the dummy local version `99.99.99` |
| Tag after publish | `PushVersionTagModule` tags `v{semver}` after the packages are on nuget.org; "tag exists" is not an error |
| Release notes | `GenerateReleaseNotes` from the previous tag, grouped by labels in `.github/release.yml` |
| Trusted publishing | `NuGet/login` with `id-token: write`, no stored API key |
| Diagnostics | Uploads hang dumps, MTP diag logs, and received snapshots on failure |
| Dependencies | Renovate with a written reason for each pinned package |

## Decision

### Version: 4.0.0

Since `3.0.0` there are 473 commits (92 `feat`, 109 `fix`). The install model
changes in ways that a 3.x user sees:

- The Inno installer is gone; Velopack `Setup.exe` installs the Daemon under `%LOCALAPPDATA%\RevitDevTool\`.
- The MCP command path moves to `%LOCALAPPDATA%\RevitDevTool\current\DevTools.Daemon.exe`.
- The bundle zip has no Daemon and no TestRunner; `PackageContents.xml` is generated from the selection.
- 3.0.0 has no updater, so a 3.x user must run the new `Setup.exe` one time.

These are breaking changes for install, MCP configuration, and offline use.
The next stable version is **4.0.0**. Before it, prereleases
`4.0.0-beta.N` go to the `prerelease` channel.

### Channels

| Channel | Source | Who gets it |
|---------|--------|-------------|
| `stable` | Tag `X.Y.Z` → GitHub release, not prerelease | Default for all users; GitHub `releases/latest` |
| `prerelease` | Tag `X.Y.Z-beta.N` → GitHub prerelease, Velopack channel `prerelease` | Users who install a beta `Setup.exe` |
| CI build | Push to `develop` or a PR | Actions artifacts only. No GitHub release, because the update engine reads releases |

We do not copy pyRevit's rolling `ci-binaries` release. A release that the
engine can see must be a real version with a manifest and signatures.

Required engine change: the default `UpdatePolicy.Channel` comes from the
Velopack channel of the install (`VelopackLocator.Current.Channel`). Without
it, a beta install looks at the stable channel and finds no update.

### Pipeline shape

1. **`Compile.yml` (CI):** keep `pack` on pushes and PRs. Add path filters,
   `permissions: contents: read`, actions pinned by SHA, diagnostic and hang-dump
   upload on failure, and `retention-days: 3` for the large artifacts.
2. **`PublishRelease.yml` (release):**
   - Runs in `environment: release` (required reviewer, secrets `UPDATE_SIGNING_KEY` and later the Authenticode identity).
   - **Gate:** waits for a green `Compile.yml` run on the same SHA, fails closed (TUnit pattern).
   - Builds one time with the tag version. We rebuild instead of promoting the CI artifact
     because the version is compiled into the assemblies, and the CI run of a commit has a
     GitVersion prerelease version, not the tag version.
   - **Guards before upload:** the version is a valid SemVer and is not lower than the latest
     release; the manifest `schema` is supported; every `.sig` verifies with the public key in
     the Daemon `appsettings.json` (a wrong secret cannot ship a feed that no client accepts);
     every payload hash in the manifest matches its zip.
   - Draft → upload payloads → upload feeds and signatures → publish (as today).
   - Release notes: GitHub generated notes from the previous tag, grouped by `.github/release.yml` labels.
   - Fast-forward `develop` (as today).
3. **`TestReleaseInstall.yml` (new, started by `PublishRelease.yml`):** on a clean `windows-2025` runner:
   1. Install the published `Setup.exe` with `--silent`.
   2. Run `DevTools.Daemon.exe --select "Revit 2025" --select "AutoCAD 2025"`.
   3. Check `installation.json`, the payload folders, the generated `PackageContents.xml`, and that
      `current\DevTools.TestRunner.exe --version` and `DevTools.Daemon.exe --stdio` start.
   4. For an update release, install the previous release first and check that the engine moves
      the payloads and Velopack moves the Daemon to the new version.

   A failure files an issue and marks the release in the notes; it does not delete the release.
4. **Host load test (later):** a self-hosted runner with Revit and AutoCAD, as pyRevit does,
   loads each payload and reads the journal. It is not required for 4.0.0.
5. **`PublishTestAdapter.yml`:** unchanged; it is a separate product version.

### When each workflow runs

| Moment | Workflow | Runner, environment | Does | Publishes |
|--------|----------|---------------------|------|-----------|
| Push to `develop` or `main` | `Compile.yml` | `windows-2025` | `pack` without a signing key; `VerifyReleaseModule` checks hashes and feeds | Actions artifacts, 3 days |
| Push to `develop` that changes site files | `Docs.yml` (build) | `ubuntu-latest` | Builds Docusaurus | Nothing |
| Pull request (also Dependabot) | `Compile.yml`; `Docs.yml` if site files change | as above | Same checks on the PR head | Nothing |
| Tag push, or dispatch with a version | `PublishRelease.yml` | `windows-2025`, `release` environment (reviewer, `UPDATE_SIGNING_KEY`) | Waits for the `Compile.yml` push run of the commit, `pack publish`, required signatures | GitHub release: draft, then published |
| End of `PublishRelease.yml`, or dispatch with a tag | `TestReleaseInstall.yml` | clean `windows-2025` | Installs the published `Setup.exe`, `--select`, checks the install | An issue if it fails |
| End of `PublishRelease.yml` for a stable tag, or dispatch | `Docs.yml` (deploy) | `ubuntu-latest`, `github-pages` environment | Builds and deploys the site from the tag | GitHub Pages |
| Manual dispatch | `PublishTestAdapter.yml` | `windows-2025` | Packs the TestAdapter | nuget.org (OIDC) |
| Weekly, GitHub-managed | Dependabot (GitHub Actions) | GitHub | One grouped PR to update pinned actions; rebases it when `develop` changes | A pull request |

A commit runs `Compile.yml` once: a push to `develop` or `main`, or a pull request for other branches.
A Dependabot rebase is a new PR commit, so its checks run again.
A release made with `GITHUB_TOKEN` does not start `release` triggers, so `PublishRelease.yml`
dispatches the follow-up workflows.

### Hygiene for all workflows

Actions pinned by SHA with the tag in a comment; `permissions` per job; a fork
guard on jobs that publish; `concurrency` groups; Dependabot for NuGet and GitHub
Actions. The per-host-version Autodesk API packages are excluded from Dependabot.

### 3.0.0 → 4.0.0 migration

3.0.0 is the only released install to migrate. Unreleased intermediate states get no
compatibility code. The first run of a Velopack-installed Daemon finds the 3.0.0 Inno
install by its HKCU uninstall entry (`{B2BC2881-A08A-41D8-B1B3-424045E529DB}_is1`). Then it:

1. Stops the Daemon processes that run from `Contents\DevTools.Daemon.exe`. The old tray
   Daemon holds the single-instance mutex.
2. Deletes `Contents\DevTools.Daemon.exe`, `Contents\DevTools.TestRunner.exe`, the Inno
   `Uninstall` folder, the two Start menu shortcuts, and the uninstall entry. The payload folders stay.
3. The install hook already replaced the Run value `DevToolsDaemon` with the Velopack stub.
4. The Updates tab selects the payloads that 3.0.0 left on disk by default.

There is no stdio forwarder at the old MCP path. The 4.0.0 release notes tell 3.0.0
users to copy the new MCP snippet from Settings one time.

### Channel choice in the UI

The Updates tab has **Include pre-releases** (off by default; on for a Setup from a
prerelease). When on, the engine takes the newest release of both channels. Velopack uses
the channel of the release it updates to, so a user can move to a prerelease and back.

## Alternatives Considered

1. **Version 3.1.0.** Fits the count of features, but hides the install and MCP-path breaks.
2. **Promote the CI artifact without a rebuild (pyRevit).** Gives byte-identical output, but needs a
   CI run on the tag with the tag version and about 900 MB of artifacts per run. Possible later
   if the version moves out of the assemblies.
3. **Rolling WIP release (pyRevit `ci-binaries`).** The update engine would see it. Actions
   artifacts give the same test builds without that risk.
4. **Release on dispatch from `main` and tag after publish (TUnit).** Good for libraries without an
   updater. Here the tag must exist before the release, because Velopack deltas and the
   manifest `notesUrl` use it.

## Consequences

Positive:

- A release cannot ship feeds that clients refuse, or a version lower than the current one.
- A published release is installed and checked on a clean machine without manual work.
- Beta testers get updates through the same engine before stable users.

Tradeoffs:

- Two more workflows (`TestReleaseInstall.yml`, Renovate) and a protected environment to maintain.
- The release still rebuilds; CI and release use the same `pack` to keep the gap small.
- 3.x users must install 4.0.0 by hand one time.

## Follow-Up

- Done 2026-10-26: default channel from the Velopack install, **Include pre-releases**,
  3.0.0 migration, `VerifyReleaseModule`, CI gate and `environment: release` in
  `PublishRelease.yml`, `TestReleaseInstall.yml`, actions pinned by SHA, `.github/release.yml`,
  Dependabot (not Renovate: no app to install), `GitVersion.yml` `next-version: 4.0.0`
  (with the `ConfiguredNextVersion` strategy), `scripts/new-update-signing-key.ps1`.
- Owner steps: run `scripts/new-update-signing-key.ps1` and commit the public key; add a required
  reviewer to the `release` environment; create the `breaking-change` label.
- Release `4.0.0-beta.1`, then `4.0.0-beta.2` to prove self-update and payload update, then `4.0.0`.
- After `4.0.0` is tagged, remove `next-version` from `GitVersion.yml`.
