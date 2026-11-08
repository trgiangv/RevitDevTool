# 0059 Four Independent Release Trains and One-Gate CI

Date: 2026-11-05

## Status

**Accepted 2026-11-08** (proposed 2026-11-05). A clean redesign: shipped Daemons,
adapters, and plugins are not carried forward. It replaces
[0054](0054-release-automation-channels-and-4-0-cutover.md), the release
discovery of [0048](0048-daemon-hosted-update-engine.md), the "one version"
rules of [0046](0046-per-year-release-payloads-and-update-manifest.md), and
§5, §6, and §7 of [0055](0055-product-version-wire-contracts-and-satellite-compatibility.md).
The contract ranges of 0055 (§1–§4, §8–§10) stay: they are what makes
independent releases safe.

Amended 2026-11-07: §6 adds the `PublicAPI.Unshipped.txt` move at a stable `addin` release ([0060](0060-framework-runtime-shims-ship-with-nettest.md) §8).

Amended 2026-11-08, at acceptance: §1 lists the framework runtimes in `nettest` and
which train owns each testing piece (0060 §1); §5 adds the `0.x` rule and the
framework floor and major rules (0060 §9); §11 names `develop` as the trunk. Later the
same day, from the implementation plan: §5 local version form and no solution-wide
version, §6 the bump PR moves `PublicAPI`, §7 which job holds no secret. Then, by
the owner: a clean cut inside the repo too; the first train releases are the
baseline (§5 refusal, "What is removed").

## Context

### Components and how they ship today

| Component | Ships through | Version today |
|-----------|---------------|---------------|
| Add-in payloads (`Contents\<year>\`) | GitHub Release `X.Y.Z`, applied by the Daemon update engine | Product version |
| Daemon (`DevTools.Daemon`, `DevTools.Update`) | The same release, Velopack package | Product version |
| TestRunner (`DevTools.TestRunner`) | Inside the Daemon Velopack package | Product version |
| TestAdapter (`RevitDevTool.TestAdapter`) | nuget.org, `PublishTestAdapter.yml` | Own `<Version>` |
| PyTest (`revitdevtool_pytest`) | PyPI, from the `RevitDevTool.PyTest` repo | Own `version` |

What ties them together today:

- One tag stamps one version on the Daemon, the TestRunner, and every payload.
  `UpdateEngine` picks payloads by `manifest.Version` (`UpdateEngine.cs:191`), so
  a Daemon-only release makes every user download every year payload again.
- The TestRunner rides in the Daemon package only for its location
  (`MergeSharedRuntime`, `AppUtils.GetTestRunnerExePath()`). Its only client is
  the TestAdapter.
- `GitHubReleaseProvider` finds releases with `Release.GetLatest` or the first
  non-draft of the last 10 releases. Any second kind of release in the repo hides
  the product release.
- PyTest lives in another repo; its wire models are synced by hand.
- GitVersion computes CI versions that look like real prereleases.

### CI today

Read from `gh run list` and `.github/workflows/` on 2026-11-05:

- `Compile.yml` runs `dotnet run -- pack` on every push and PR, with no path
  filter on pushes, and runs no tests.
- One release is three runs (`Publish release`, `Test release install`, `Docs`),
  chained with `gh workflow run`. The release builds again.
- `PublishTestAdapter.yml` publishes without tests, pins actions by major tag, and
  has no environment.
- Every Dependabot PR runs `Compile` and `Docs`.
- Commits follow Conventional Commits and land by direct push, so PR-label release
  notes see almost nothing. `Changelog.md` stops at `3.0.0`.

## Decision

### 1. Four trains, one shape

| Train | Contains | Version file | Tag | Registry or feed |
|-------|----------|--------------|-----|------------------|
| `daemon` | Daemon, update engine, `RevitDevTool-Setup.exe` | `<Version>` in `source/DevTools.Daemon/DevTools.Daemon.csproj` | `daemon/vX.Y.Z` | Velopack feed, `channels.json` |
| `addin` | Revit and AutoCAD add-ins, one payload per year, offline bundle | `<Version>` in `source/AddinVersion.props` (imported by the host projects) | `addin/vX.Y.Z` | `update-manifest.json`, `channels.json` |
| `nettest` | NuGet `RevitDevTool.TestAdapter`: adapter, MTP providers, framework runtimes and their manifests (0060), TestRunner | `<Version>` in `source/DevTools.TestAdapter/DevTools.TestAdapter.csproj` | `nettest/vX.Y.Z` | nuget.org |
| `pytest` | PyPI `revitdevtool_pytest` | `version` in `source/RevitDevTool.PyTest/pyproject.toml` | `pytest/vX.Y.Z` | PyPI |

Every train has the same life: a version file bumped in a PR, a CI job, one
release run, a GitHub Release with its notes, a tag, and a changelog file. The
release tooling knows no special train.

- Each train releases only when its own code changed. Shared `DevTools.*` code
  belongs to every train that builds it, and its change is a reason to release
  each of them, not a train of its own.
- Testing pieces follow 0060 §1: the kernel (`DevTools.Testing.Host`) is `addin`;
  the framework runtimes, MTP providers, adapter, and runner are `nettest`;
  `DevTools.Testing.Abstractions`, `DevTools.Testing.Transport`, and
  `props/TestingRuntime.props` are shared by both.
- Compatibility between trains is decided by the 0055 contract ranges. The one
  version rule across trains is `minUpdaterVersion` in the add-in manifest: the
  oldest Daemon that can install the payload (0052).
- A stable release never requires a prerelease of another train.
- "NetTest" names the train. The NuGet id stays `RevitDevTool.TestAdapter`.

### 2. Discovery through a signed channel index

The Daemon stops reading the GitHub release list. Each `daemon` and `addin`
release updates one file, `channels.json`, signed like the feeds (0051):

```json
{
  "schema": 1,
  "stable": {
    "daemon": { "version": "4.1.0", "feed": ".../releases/download/daemon/v4.1.0/" },
    "addin":  { "version": "4.1.0", "manifest": ".../releases/download/addin/v4.1.0/update-manifest.json" }
  },
  "prerelease": {
    "daemon": { "version": "4.2.0-beta.1", "feed": "..." },
    "addin":  { "version": "4.2.0-beta.2", "manifest": "..." }
  }
}
```

- It is the asset of one rolling GitHub Release, `channels`, replaced by every
  `daemon` and `addin` release. Clients read it from a fixed download URL: one
  request, no REST API, no rate limit.
- `prerelease` always names the newest of stable and prerelease, so a prerelease
  install moves to stable when stable is newer.
- The `daemon` entry points the Velopack update source at that release. The
  `addin` entry points the update engine at its manifest. Payload choice compares
  the add-in version with the installed one per year.
- Only stable `daemon` releases are marked "latest" on GitHub, so
  `releases/latest/download/RevitDevTool-Setup.exe` stays the installer link.
  Every other release sets `make_latest: false`.

### 3. TestRunner ships in the NetTest package

- Framework-dependent, single-file, under `tools/runner/` of the nupkg. Every
  machine that runs `dotnet test` has the .NET SDK.
- `net10.0-windows` with `RollForward=Major`. It moves to `net8.0-windows` if users
  report a missing runtime, after `DevTools.Hosting*` gain that target. A missing
  runtime ends with one sentence: "RevitDevTool.TestAdapter needs the .NET <major>
  runtime or later."
- The runner is the one inside the package, named by the `devtools.runnerPath`
  assembly metadata the package targets write. There is no override and no lookup
  in the Daemon folder (amended 2026-11-08; the environment variable is gone).
- The runner leaves the Daemon package now. `MergeSharedRuntime` and the
  `VelopackPublish` branch of the runner project go away. Native AOT is refused:
  `VisualStudioAttach` uses EnvDTE and `[ComImport]` interop.

### 4. PyTest moves into this repo

- `source/RevitDevTool.PyTest/`, history kept (`git subtree` or `git filter-repo`).
- pixi tasks `lint`, `test`, `build`. CI uses `prefix-dev/setup-pixi`.
- Golden JSON fixtures for the pytest wire are read by both the C# tests and the
  pytest tests.
- The PyPI Trusted Publisher is registered for this repo, `release.yml`, and
  environment `pytest`. The old repo is archived with a pointer.

### 5. Version format

| Train | Grammar | Examples |
|-------|---------|----------|
| `daemon`, `addin`, `nettest` | SemVer 2.0: `MAJOR.MINOR.PATCH[-LABEL.N]`, `LABEL` ∈ `alpha`, `beta`, `rc` | `4.1.0-beta.2`, `4.1.0`, `0.4.0-rc.1` |
| `pytest` | PEP 440 subset: `MAJOR.MINOR.PATCH[{a\|b\|rc}N]` | `0.8.0b1`, `0.8.0` |

- The tag is `<train>/v` plus the registry string as is: `addin/v4.1.0-rc.1`,
  `pytest/v0.8.0b1`. Labels map one to one: `alpha` = `a`, `beta` = `b`, `rc` = `rc`.
- `daemon` and `addin` both start at `4.0.0` and then move on their own.
- `+<sha>` appears only in `InformationalVersion`.
- **Non-release builds** use `0.0.0`: `0.0.0-ci.<run>+<sha>` in CI,
  `0.0.0-local.<UTC yyyyMMddHHmmss>+<sha>` on a dev machine (NuGet ignores `+…`, so the
  stamp keeps local packs apart), `0.0.0.dev<run>` for `pytest`. `publish` refuses
  `0.0.0`. A local pack cannot collide with a published or an earlier local package, so
  the NuGet cache workaround in `PackTestAdapterModule` goes away.
- No version is passed to the whole solution: each train's entry projects read its
  version file, and the product version a process reports is its own train's.
- **GitVersion is dropped.** The version file is the only source.

| Part | When |
|------|------|
| `MAJOR` | A contract `Min` rises (0055 §3), or a user-facing feature is removed |
| `MINOR` | A new feature, or a contract `Current` rises |
| `PATCH` | Fixes only |
| `-LABEL.N` | Labels go `alpha` → `beta` → `rc`, never back. `N` starts at 1 per label |

- While `MAJOR` is `0`, a change that would raise `MAJOR` raises `MINOR`.
- `nettest` only: raising a framework floor (0060 §9) raises `MINOR`; dropping a
  supported framework major raises `MAJOR`.

`release <train>` refuses a version that does not match the grammar, is not
greater than the train's latest `<train>/v*` tag, goes back a label, is stable
without a changelog section, or is stable and needs a prerelease of another train.
It warns when a contract `Min` changed since the last tag and `MAJOR` did not.

### 6. Prerelease flow

The same steps for every train:

1. Set the prerelease version in the version file, with the changelog, in a PR.
2. Run `release.yml` for the train. Notes come from the edited section, or from the
   generated draft since the train's previous tag.
3. Repeat for more betas. Between prereleases a contract `Min` may rise (0055 §3).
4. `-rc.N` freezes features: a `feat` commit on the train's paths after an `rc` tag
   gets a CI warning.
5. The stable version needs the edited changelog section.

The bump PR of a stable `addin` release also moves every line of
`source/DevTools.Testing.Abstractions/PublicAPI.Unshipped.txt` into
`PublicAPI.Shipped.txt` and leaves `Unshipped` empty; `release addin` refuses a stable
version while `Unshipped` has lines. A `*REMOVED*` line is resolved
there and is allowed only when `MAJOR` rises (0055 §3).

Where the prerelease goes:

| Train | Prerelease reaches | Opt-in |
|-------|--------------------|--------|
| `daemon`, `addin` | `channels.json` → `prerelease` | The install's channel (0054 UI) |
| `nettest` | nuget.org prerelease (from `-beta.N`) | `--prerelease` or an explicit version |
| `pytest` | PyPI prerelease (from `bN`) | `pip --pre`, or the pixi and uv prerelease setting |

**Order across trains.** A provider ships before its consumers: `daemon` when the
add-in needs a newer `minUpdaterVersion`, then `addin`, then `nettest` and
`pytest`. The newer host explains a mismatch to an older client (0055 §10).

**Hotfix while a prerelease is open.** Create `release/<train>/X.Y` from the
stable tag, fix, release from that branch, and cherry-pick to trunk. The stable
fix stays below the open prerelease, so both channels keep their order.

### 7. One release workflow

`release.yml`, started by `workflow_dispatch` with one input: `train`.
`run-name: release ${{ inputs.train }}` makes every run readable.

| Job | Runs for | Does |
|-----|----------|------|
| `validate` | all | `dotnet run --project build -- release <train> --dry-run`: version rules (§5), changelog section, what will ship |
| `build` | all | Tests of the train, then the train's artifacts. No secrets. Uploads the artifact |
| `publish` | all, `environment: <train>` | `daemon`, `addin`: sign feeds, draft, upload payloads then feeds, publish. `nettest`: `NuGet/login` (OIDC) and push. `pytest`: `pypa/gh-action-pypi-publish` (OIDC). Skips a version the registry already has. GitHub Release and tag. Provenance attestation |
| `channels` | `daemon`, `addin` | Rewrite and sign `channels.json`, replace it in the `channels` release |
| `verify-install` | `daemon`, `addin` | Clean runner: install `Setup.exe` from `channels.json`, `--select`, check `installation.json`; issue on failure |

- One run per release. Follow-up work is a `needs:` job.
- The job that compiles and tests holds no secret; `publish` runs only the publish
  step of the same commit, in the train's protected environment. Each environment holds
  only its train's secrets: the signing key in `daemon` and `addin`, nothing stored in
  `nettest` and `pytest` (OIDC).
- The Velopack delta is built against the previous `daemon/v*` release, downloaded
  by tag. A release holds only the delta from the version before it, so the Daemon's
  update source keeps a delta only when the feed lists the full package of the
  installed version; otherwise Velopack takes the full package. Without that filter
  Velopack applies the one delta to an older base and leaves stale files (verified
  2026-11-08).

### 8. CI

`ci.yml` on `pull_request`, push to trunk, and `merge_group`:

| Job | Paths | Does |
|-----|-------|------|
| `changes` | all | Path filter from the train declarations in `build/` |
| `daemon` | Daemon train paths | Compile, host-free tests; on trunk push also `Setup.exe` smoke pack |
| `addin` | Add-in train paths | Compile every year configuration, host-free tests; on trunk push also payload smoke pack |
| `nettest` | NetTest train paths | Adapter and runner tests, pack `0.0.0-ci` nupkg |
| `pytest` | PyTest train paths and golden fixtures | pixi lint, test, build `0.0.0.dev` wheel, golden contract |
| `docs` | `docs/**` | Typecheck and build; on trunk push deploy to Pages (`environment: github-pages`) |
| `workflows` | `.github/**` | `actionlint`, `zizmor` |
| `gate` | always | `re-actors/alls-green`. **The only required check** |

- A PR that changes a train's paths without changing its version file gets a
  warning, not a failure.
- A `!` commit in a train whose next version is not a new major gets a warning.
- `permissions: {}` at workflow level, granted per job. Actions pinned by SHA.
  Dependabot groups updates per ecosystem, weekly.

### 9. Changelog

| Train | File |
|-------|------|
| `daemon` | `source/DevTools.Daemon/CHANGELOG.md` |
| `addin` | `source/RevitDevTool/CHANGELOG.md` |
| `nettest` | `source/DevTools.TestAdapter/CHANGELOG.md` |
| `pytest` | `source/RevitDevTool.PyTest/CHANGELOG.md` |

All use Keep a Changelog. The root `Changelog.md` is deleted; the root README links
the four.

1. **Draft.** `changelog <train>` runs `git-cliff` (installed with pixi) over the
   train's paths since its last tag and writes `## [Unreleased]`. A commit goes to
   every train whose paths it touches. The commit type picks the section.
   `chore`, `ci`, `test`, and `docs` commits are left out.
2. **Edit.** A person rewrites the draft for that train's user and renames
   `Unreleased` to the version in the bump PR.
3. **Read.** The release reads the version's section. Stable without one fails.
   A prerelease without one uses the draft.
4. **Publish.** The GitHub Release body for every train, plus
   `PackageReleaseNotes` (`nettest`) and `project.urls.Changelog` (`pytest`).

### 10. `build/` is the one release tool

`dotnet run --project build -- <target> [train]`. YAML holds triggers,
permissions, environments, and OIDC steps.

| Target | Does |
|--------|------|
| `ci <train>` | Compile and host-free tests of the train |
| `pack <train>` | Artifacts with a `0.0.0` version |
| `release <train> [--dry-run]` | Validate, test, build release artifacts |
| `publish <train>` | Upload, GitHub Release, tag |
| `channels` | Write and sign `channels.json` |
| `changelog <train>` | Write the `Unreleased` draft |

- Each train is declared once in `build/`: paths, version file, tag pattern,
  changelog file, artifacts. CI path filters, the bump warning, and the changelog
  all read that declaration.
- `pytest` keeps its toolchain in pixi; `build/` calls the pixi tasks.
- Every target runs locally. `--dry-run` shows what a release would ship.

### 11. One trunk

Releases are cut from `develop`, the default branch. `main`, which only marked
releases, and the "fast-forward develop" step are retired: a release is marked by
its `<train>/v*` tag. `release/<train>/X.Y` branches exist only
for hotfixes (§6).

### What is removed

GitVersion and `GitVersion.yml`; `GitHubReleaseProvider` release-list discovery;
the shared product tag and the "one version across years" rule; the TestRunner in
the Daemon package and the lookup in the Daemon folder; `Compile.yml`,
`PublishRelease.yml`, `PublishTestAdapter.yml`, `TestReleaseInstall.yml`, and the
`Docs.yml` (its build and deploy move into `ci.yml`); `.github/release.yml` label categories; the separate
`RevitDevTool.PyTest` repo; the 3.0.0 Inno Setup cleanup in the Daemon; the root
`Changelog.md`.

The first release of each train is the baseline. Releases, tags, and registry
versions from before the trains are history: the release tool reads only
`<train>/v*` tags, and nothing in the repo keeps an old path working beside the
new one.

## Alternatives Considered

1. **Lockstep version for everything (TUnit).** One number, but every release
   republishes unchanged packages and makes users download unchanged payloads.
2. **One distribution release for Daemon and add-in, with per-component versions
   and asset reuse.** Kept the GitHub "latest" lookup working for shipped Daemons.
   Without that constraint it is a harder version of §1: change detection by path,
   assets pointing at older releases, and two version notions in one release.
3. **No GitHub Release for NetTest and PyTest.** Needed only while the Daemon read
   the release list. With `channels.json`, every train gets visible notes.
4. **`channels.json` on GitHub Pages.** Works, but ties update publishing to the
   docs deploy. The rolling `channels` release keeps it in the release run.
5. **Four release workflows.** Same jobs four times. One workflow with a `train`
   input and per-train environments reads the same and changes in one place.
6. **Changesets or release-please.** Release PRs and per-package changelogs, but
   Changesets needs Node, and both add a second version mechanism beside the
   version files.
7. **TestRunner self-contained in the nupkg, as its own Velopack package, or as
   Native AOT.** 70 MB per version; a second feed and install folder; EnvDTE and
   COM interop do not run under AOT.
8. **GitHub generated notes from PR labels.** Most commits are direct pushes, and
   the notes do not split by train.
9. **Keep GitVersion.** It needs a tag prefix and branch config per train, and its
   CI versions look like real prereleases. The version file plus `0.0.0` builds is
   simpler.
10. **SemVer in the PyTest version file, converted at build time.** The file, the
    wheel, and PyPI would show different strings.

## Consequences

Positive:

- A change releases only the trains it touches. Users download only what changed.
- All four trains follow one runbook and one workflow.
- The Daemon finds updates with one signed request, not the REST API.
- One required check; docs-only and workflow-only changes skip .NET.
- CI runs host-free tests for every train, and every release tests before it
  publishes.
- The pytest contract is checked on both sides in the same PR.

Tradeoffs:

- No upgrade path from what is shipped today: `4.0.0-beta.*` Daemons, adapters
  `0.2.x`, and plugins `0.6.x` must be reinstalled or updated by hand once.
- A change in shared `DevTools.*` code can mean three releases. The bump warning
  names them, and the order rule (§6) says which goes first.
- The `channels` release is a single point that every install reads. A bad write
  is fixed by rerunning `channels` from the previous release.
- Support must ask for two versions (Daemon and add-in); both are shown in the UI.
- Python joins the repo toolchain.

## Follow-Up

- Plan in `docs/plans/active/`: train declarations in `build/` and `0.0.0`
  builds; `ci.yml` and `gate`; `release.yml` for `nettest` (smallest); PyTest move
  and `pytest`; `channels.json` and the update engine; `daemon` and `addin`;
  changelog targets; delete the old workflows.
- Update `docs/agents/` build and test-matrix pages once `ci <train>` exists.
