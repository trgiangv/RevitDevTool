# Execution Plan: Four Release Trains and One-Gate CI (ADR 0059)

Date: 2026-11-06

## Status

Active. Nothing implemented yet. Decision: [0059](../../decisions/0059-independent-release-trains-and-one-gate-ci.md)
(Proposed). Companion plan: [2026-11-06-framework-shims-nettest-0060](../completed/2026-11-06-framework-shims-nettest-0060.md);
the first `nettest` release ships both plans together (see Release order).

Visual of the agreed flow (owner's private artifact):
https://claude.ai/artifact/UQFpzx18qhrfoaqWMdfnvJ

## Outcome

1. Four trains, one shape: `daemon`, `addin`, `nettest`, `pytest`. Each has a
   version file, a CI job, one `release.yml` run, a GitHub Release, a tag
   `<train>/vX.Y.Z`, and a `CHANGELOG.md`.
2. The Daemon finds `daemon` and `addin` updates through a signed `channels.json`,
   not the GitHub release list.
3. The TestRunner ships inside the `nettest` nupkg; the Daemon package no longer
   carries it.
4. `RevitDevTool.PyTest` lives in `source/RevitDevTool.PyTest/`, built with pixi.
5. `ci.yml` has one required check (`gate`); docs-only and workflow-only changes
   skip .NET.
6. The old workflows, GitVersion, and the shared product tag are gone.

## Context

- Decision: 0059 (this plan implements it). Builds on 0046–0055. Keeps the 0055
  contract ranges (§1–§4, §8–§10) unchanged.
- Today's pipeline (read 2026-11-05): `.github/workflows/{Compile,Docs,PublishRelease,PublishTestAdapter,TestReleaseInstall}.yml`;
  `build/Program.cs` (flags via `args.Contains`), `build/Modules/*`,
  `build/appsettings.json`, `GitVersion.yml`.
- Coupling facts that drove the design (verified in code 2026-11-05):
  - `source/DevTools.Update/Engine/UpdateEngine.cs:191` picks payloads by
    `manifest.Version`.
  - `source/DevTools.Update/Releases/GitHubReleaseProvider.cs:28-38` uses
    `Release.GetLatest` / first non-draft of 10.
  - `build/Modules/PackDaemonModule.cs` `MergeSharedRuntime` merges the TestRunner
    into the Daemon folder; `source/DevTools.TestAdapter/TestingRunnerPaths.cs:21`
    finds it via `source/DevTools.Utilities/AppUtils.cs:29` (`%LOCALAPPDATA%\RevitDevTool\current`).
  - `build/Modules/ResolveVersionModule.cs` `git describe --tags` (would pick any tag).
  - `Compile.yml` runs `pack`, not `test` (`build/Program.cs`: `TestProjectModule`
    only with `test`).
  - `build/Modules/PackTestAdapterModule.cs` deletes the extracted package from the
    NuGet cache because local packs reuse the published version.
  - Commits are Conventional Commits and mostly direct pushes; `.github/release.yml`
    label notes see almost nothing; `Changelog.md` stops at `3.0.0`.
- Proof commands: `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  `docs/agents/mcp-integration-test.md`.

## Locked decisions (do not re-open without the owner)

Agreed with the owner in the 2026-11-05/06 session. Each line says why.

| # | Decision | Why |
|---|----------|-----|
| L1 | **No legacy.** Shipped `4.0.0-beta.*` Daemons, adapters `0.2.x`, plugins `0.6.x` get no automatic path; users reinstall or update by hand once. | Owner: "tổng thể flow có thể thay đổi lớn, ko cần giữ legacy". Removing this constraint removed most of the complexity |
| L2 | Four trains: `daemon`, `addin`, `nettest`, `pytest`. A train releases only when its own code changed. | Owner: a small TestRunner/add-in change must not force other components to version |
| L3 | `nettest` is the train name; NuGet id stays `RevitDevTool.TestAdapter`. `nettest` and `pytest` mirror each other in every step. | Owner asked to rename for symmetry with `pytest`. A new package id would strand consumers |
| L4 | Tag `<train>/v<registry string>` for all trains. Every train gets a GitHub Release. Only stable `daemon` is marked latest (`make_latest`), so `releases/latest/download/RevitDevTool-Setup.exe` stays the installer link. | Uniform; possible once discovery no longer reads the release list |
| L5 | Discovery through signed `channels.json`, the asset of a rolling GitHub Release named `channels`, rewritten by every `daemon`/`addin` release. `prerelease` names the newest of both kinds. | One request, no REST API or rate limit; decouples from GitHub "latest" |
| L6 | Only cross-train version rule: `minUpdaterVersion` in the add-in manifest. Everything else is 0055 contract ranges. A stable release never requires a prerelease of another train. | Keeps trains independent |
| L7 | Version file is the only source; GitVersion dropped. `daemon`: `<Version>` in `source/DevTools.Daemon/DevTools.Daemon.csproj`. `addin`: `<Version>` in new `source/AddinVersion.props` imported by the host projects. `nettest`: `DevTools.TestAdapter.csproj`. `pytest`: `pyproject.toml`. `daemon` and `addin` both start at `4.0.0`. | Explicit, reviewable bumps; no CI version that looks like a prerelease |
| L8 | Grammar: SemVer `MAJOR.MINOR.PATCH[-alpha|beta|rc.N]` for `daemon`/`addin`/`nettest`; PEP 440 subset `MAJOR.MINOR.PATCH[a|b|rcN]` for `pytest`. Labels map 1:1 and never go back. | Registry string is the version; no SemVer↔PEP 440 conversion |
| L9 | Non-release builds are `0.0.0-ci.<run>+<sha>`, `0.0.0-local+<sha>`, `0.0.0.dev<run>` (pytest). `publish` refuses `0.0.0`. | A local pack never collides with a published package; removes the NuGet-cache workaround |
| L10 | Bump rules: `MAJOR` = contract `Min` rises or a user feature is removed; `MINOR` = feature or `Current` rises; `PATCH` = fixes. | 0055 §3 |
| L11 | One `release.yml`, `workflow_dispatch`, single input `train`, `run-name: release <train>`. Jobs: `validate` → `build` (no secrets) → `publish` (`environment: <train>`) → `channels` and `verify-install` (daemon/addin only). Environments are named after trains: `daemon`, `addin`, `nettest`, `pytest`, plus `github-pages`. | One place to change; readable runs; per-train secrets |
| L12 | `ci.yml`: `changes` → `daemon`, `addin`, `nettest`, `pytest`, `docs` (deploys on trunk push), `workflows` (`actionlint`, `zizmor`) → `gate` (`re-actors/alls-green`), the only required check. Warnings, not failures: train paths changed without a version bump; a `!` commit without a major. | Less noise; one required check with path filters |
| L13 | TestRunner: framework-dependent single-file under `tools/runner/` of the nupkg, `net10.0-windows` + `RollForward=Major`. Lookup: `DEVTOOLS_TESTING_RUNNER_PATH`, then the package. No Daemon-folder lookup. Missing runtime → one sentence. | Every `dotnet test` machine has the SDK (owner confirmed). AOT refused (EnvDTE/`[ComImport]`); self-contained (~70 MB) and own Velopack package refused |
| L14 | PyTest moves into `source/RevitDevTool.PyTest/` with history; pixi tasks `lint`, `test`, `build`; golden JSON fixtures shared with the C# tests. | Contract drift becomes a CI failure |
| L15 | Changelog: one Keep a Changelog file per train (`source/DevTools.Daemon/CHANGELOG.md`, `source/RevitDevTool/CHANGELOG.md`, `source/DevTools.TestAdapter/CHANGELOG.md`, `source/RevitDevTool.PyTest/CHANGELOG.md`); root `Changelog.md` becomes an index. Draft from commits with `git-cliff` (via pixi) over the train's paths; a person edits; stable without a section fails; prerelease falls back to the draft. | Commits, not PR labels, are the source here |
| L16 | `build/` is the one release tool: `dotnet run --project build -- <target> [train]` with targets `ci`, `pack`, `release [--dry-run]`, `publish`, `channels`, `changelog`. Each train declared once (paths, version file, tag pattern, changelog, artifacts); CI path filters, bump warning, and changelog read that declaration. YAML holds triggers, permissions, environments, OIDC steps only. | TUnit/pyRevit pattern already used here |
| L17 | One trunk branch; `release/<train>/X.Y` only for hotfixes. Order across trains: provider first (`daemon` if `minUpdaterVersion` moves, then `addin`, then `nettest`/`pytest`). | Trunk-based; 0055 §10 newer side explains |
| L18 | Keep it safe-enough, not over-engineered. | Owner's standing guidance |

Rejected (with reason) — do not propose again:

- Lockstep version for all (TUnit style): republishes unchanged packages.
- One "distribution release" for Daemon + add-in with per-component versions and
  asset reuse: only needed for shipped Daemons (L1 removed that need).
- No GitHub Release for satellites: only needed while the Daemon read the list.
- `channels.json` on GitHub Pages: ties updates to the docs deploy.
- Four release workflows; Changesets; release-please; keeping GitVersion; PR-label
  release notes; SemVer in `pyproject.toml` converted at build.

## Open questions (owner decides; ask before the phase that needs it)

- **Q1 Trunk name** (`develop` or `main`). Needed in P2.
- **Q2 Velopack update source** for `daemon`: `SimpleWebSource` on the release
  download URL (a client more than one version behind takes the full package) or
  a custom `IUpdateSource` that resolves assets across `daemon/v*` releases.
  Spike in P0 before P5.
- **Q3 First versions** under the new scheme: `daemon/v4.0.0` and `addin/v4.0.0`
  stable, or a `-beta.1` round first.

## Scope

In scope: everything in 0059 §1–§11 and "What is removed".

Out of scope: the framework runtime move (plan 0060); McpGateway; changes to 0055 ranges;
`LegacyInnoInstall` cleanup (separate, optional).

## Approach

Phases are ordered so each lands green on its own. `nettest` goes first because it
is the smallest train and exercises the whole `release.yml` shape.

### P0 Spikes (no product change)

- Velopack source (Q2): try `SimpleWebSource` against a release download URL with
  a feed that lists prior deltas; record what happens two versions behind.
- `vpk download` of the previous `daemon/v*` release by tag (for the delta).
- `git-cliff --include-path` + `--tag-pattern 'nettest/v*'` produces a per-train
  draft from this repo's history.

### P1 `build/` trains and versions

- Train declarations (one type in `build/`, data per train: id, input paths,
  version file + reader, tag pattern, changelog path, artifacts).
- Version readers for csproj/props `<Version>` and `pyproject.toml`; grammar
  validation (L8); `0.0.0` builds (L9); refuse `0.0.0` in publish.
- Remove GitVersion (`GitVersion.yml`, the GitVersion path in
  `ResolveVersionModule`); previous version = latest tag matching the train pattern.
- Replace `args.Contains` in `build/Program.cs` with targets + train argument (L16).
- Remove the NuGet-cache workaround in `PackTestAdapterModule` once packs are `0.0.0`.
- Create `source/AddinVersion.props` and import it in `RevitDevTool` and
  `AcadDevTool`; set `<Version>` in the Daemon csproj.

### P2 `ci.yml`

- Jobs per L12; path filter generated from (or kept in sync with) the train
  declarations; `gate` with `alls-green`.
- `permissions: {}` at workflow level; actions pinned by SHA; Dependabot grouped
  weekly per ecosystem.
- Docs deploy moves here (trunk push, `environment: github-pages`).
- Owner switches the required check to `ci / gate` (manual, repo settings).

### P3 `release.yml` with `nettest` + TestRunner in the nupkg

- `release.yml` skeleton (L11) with only `nettest` enabled.
- TestRunner single-file into `tools/runner/` (L13); `TestingRunnerPaths` lookup
  order; one-sentence missing-runtime error; drop the runner and
  `MergeSharedRuntime` from `PackDaemonModule`; drop the `VelopackPublish` branch
  in `DevTools.TestRunner.csproj`; drop runner checks in the install test.
- `nettest` changelog file.
- Owner: environment `nettest`; NuGet trusted publishing policy for this repo +
  `release.yml` + environment `nettest`.

### P4 PyTest move + `pytest` train

- `git subtree add` (or `filter-repo`) into `source/RevitDevTool.PyTest/`.
- pixi tasks `lint`, `test`, `build`; `project.urls` updated; dev-only pytest ini
  (host_name/host_version) separated from the package config.
- Golden fixtures for the pytest wire, read by C# tests (beside
  `source/DevTools.Execution/External/Testing/PytestContracts.cs`) and pytest tests.
- `pytest` enabled in `release.yml`; `ci.yml` `pytest` job.
- Owner: environment `pytest`; PyPI Trusted Publisher (repo, `release.yml`,
  environment `pytest`); archive the old repo with a pointer.

### P5 `channels.json` and the update engine

- Model + signer (reuse `ReleaseSignature`, 0051); `build -- channels` writes and
  signs it; replaces the asset of the `channels` release.
- `DevTools.Update`: replace `GitHubReleaseProvider` with a channel-index provider;
  payload choice compares the add-in version per year; Daemon update source per Q2;
  `minUpdaterVersion` gate stays.
- Tests for the provider, signature failure, and channel selection.

### P6 `daemon` and `addin` trains

- Split today's product publish into the two trains (assets per 0059 §1; `Setup.exe`
  belongs to `daemon`; payload zips, bundle zip, `update-manifest.json` to `addin`).
- Delta from the previous `daemon/v*` by tag.
- `channels` and `verify-install` jobs (install from `channels.json`).
- `make_latest` only for stable `daemon`.
- Validation rule: stable cannot require another train's prerelease.

### P7 Changelog targets

- `build -- changelog <train>` with git-cliff config per train; section read per
  train; root `Changelog.md` index.

### P8 Cleanup and docs

- Delete `Compile.yml`, `PublishRelease.yml`, `PublishTestAdapter.yml`,
  `TestReleaseInstall.yml`, the deploy part of `Docs.yml`, `.github/release.yml`
  categories.
- Update `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  `docs/agents/verification.md`, and the product/architecture pages that describe
  update discovery (0048 text) — one layer each, per `AGENTS.md`.
- Owner: tag rulesets (`daemon/*`, `addin/*`, `nettest/*`, `pytest/*`), trunk
  protection with `ci / gate`.

## Release order (first releases under the new scheme)

1. `daemon` (channel index provider, Q3 version).
2. `addin` (framework runtimes removed per plan 0060, `minUpdaterVersion` = that Daemon).
3. `nettest` with TestRunner **and** the framework runtimes of plan 0060, in one release, so users
   change their setup once.
4. `pytest` (first release from this repo).

Users of shipped versions reinstall `Setup.exe` once (L1).

## Risks And Recovery

- Bad `channels.json` write: rerun the `channels` job from the previous release.
- A train release fails mid-publish: `publish` skips versions the registry has; the
  GitHub Release stays draft until all assets upload (feeds last, as today).
- Path declarations miss an input: CI skips a job it should run. Mitigation: the
  shared `DevTools.*` paths belong to every train that builds them; review the
  declaration in P1.
- Rollback of the pipeline itself: old workflows are deleted only in P8, after the
  first release of each train succeeded.

## Progress

- [ ] P0 spikes (Q2 answer recorded in Decisions)
- [ ] P1 build trains and versions
- [ ] P2 ci.yml and gate
- [ ] P3 release.yml + nettest + TestRunner in nupkg
- [ ] P4 PyTest move + pytest train
- [ ] P5 channels.json + update engine
- [ ] P6 daemon and addin trains
- [ ] P7 changelog targets
- [ ] P8 cleanup and docs

## Decisions

- 2026-11-06: Environments are named after trains (`nettest`, `pytest`), so the
  PyPI publisher uses environment `pytest` (ADR 0059 §4 corrected).

## Validation

- Focused proof per phase: `build/` unit tests for version grammar, previous-tag
  lookup, train declarations; `dotnet run --project build -- release <train> --dry-run`
  output for each train.
- Integration: one real `release.yml` run per train to a prerelease version; the
  `verify-install` job for `daemon`/`addin`.
- Repository-required checks: compile per `.agents/skills/build/SKILL.md` after
  every `.cs`/`.csproj` edit.

## Result

Not started.
