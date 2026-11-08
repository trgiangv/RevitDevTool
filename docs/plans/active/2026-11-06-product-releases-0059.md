# Execution Plan: Four Release Products and One-Gate CI (ADR 0059)

Date: 2026-11-06

## Status

Active. P3a (TestRunner in the nupkg) landed in `d075513b` with live proof on
2026-11-08; the rest not started, specified to implementation detail 2026-11-08. Decision: [0059](../../decisions/0059-independent-product-releases-and-one-gate-ci.md)
(Accepted 2026-11-08). Companion plan, completed: [2026-11-06-framework-shims-nettest-0060](../completed/2026-11-06-framework-shims-nettest-0060.md);
the first `nettest` release ships both plans together (see Release order). This plan
also owns the release proof that [2026-10-25-auto-update](2026-10-25-auto-update.md)
used to hold.

Visual of the agreed flow (owner's private artifact):
https://claude.ai/artifact/UQFpzx18qhrfoaqWMdfnvJ

## Outcome

1. Four products, one shape: `daemon`, `addin`, `nettest`, `pytest`. Each has a
   version file, a CI job, one `release.yml` run, a GitHub Release, a tag
   `<product>/vX.Y.Z`, and a `CHANGELOG.md`.
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
| L2 | Four products: `daemon`, `addin`, `nettest`, `pytest`. A product releases only when its own code changed. | Owner: a small TestRunner/add-in change must not force other components to version |
| L3 | `nettest` is the product name; NuGet id stays `RevitDevTool.TestAdapter`. `nettest` and `pytest` mirror each other in every step. | Owner asked to rename for symmetry with `pytest`. A new package id would strand consumers |
| L4 | Tag `<product>/v<registry string>` for all products. Every product gets a GitHub Release. Only stable `daemon` is marked latest (`make_latest`), so `releases/latest/download/RevitDevTool-Setup.exe` stays the installer link. | Uniform; possible once discovery no longer reads the release list |
| L5 | Discovery through signed `channels.json`, the asset of a rolling GitHub Release named `channels`, rewritten by every `daemon`/`addin` release. `prerelease` names the newest of both kinds. | One request, no REST API or rate limit; decouples from GitHub "latest" |
| L6 | Only cross-product version rule: `minUpdaterVersion` in the add-in manifest. Everything else is 0055 contract ranges. A stable release never requires a prerelease of another product. | Keeps products independent |
| L7 | Version file is the only source; GitVersion dropped. `daemon`: `<Version>` in `source/DevTools.Daemon/DevTools.Daemon.csproj`. `addin`: `<Version>` in new `props/AddinVersion.props` imported by the host projects. `nettest`: `DevTools.TestAdapter.csproj`. `pytest`: `pyproject.toml`. `daemon` and `addin` both start at `4.0.0`. | Explicit, reviewable bumps; no CI version that looks like a prerelease |
| L8 | Grammar: SemVer `MAJOR.MINOR.PATCH[-alpha|beta|rc.N]` for `daemon`/`addin`/`nettest`; PEP 440 subset `MAJOR.MINOR.PATCH[a|b|rcN]` for `pytest`. Labels map 1:1 and never go back. | Registry string is the version; no SemVer↔PEP 440 conversion |
| L9 | Non-release builds are `0.0.0-ci.<run>+<sha>`, `0.0.0-local.<UTC stamp>+<sha>` (L24), `0.0.0.dev<run>` (pytest). `publish` refuses `0.0.0`. | A local pack never collides with a published package; removes the NuGet-cache workaround |
| L10 | Bump rules: `MAJOR` = contract `Min` rises or a user feature is removed; `MINOR` = feature or `Current` rises; `PATCH` = fixes. | 0055 §3 |
| L11 | One `release.yml`, `workflow_dispatch`, single input `product`, `run-name: release <product>`. Jobs: `validate` → `build` (no secrets) → `publish` (`environment: <product>`) → `channels` and `verify-install` (daemon/addin only). Environments are named after products: `daemon`, `addin`, `nettest`, `pytest`, plus `github-pages`. | One place to change; readable runs; per-product secrets |
| L12 | `ci.yml`: `changes` → `daemon`, `addin`, `nettest`, `pytest`, `docs` (deploys on trunk push), `workflows` (`actionlint`, `zizmor`) → `gate` (`re-actors/alls-green`), the only required check. Warnings, not failures: product paths changed without a version bump; a `!` commit without a major. | Less noise; one required check with path filters |
| L13 | TestRunner: framework-dependent single-file under `tools/runner/` of the nupkg, `net10.0-windows` + `RollForward=Major`. The runner is the package's, named by `devtools.runnerPath` metadata; no override, no Daemon-folder lookup (owner, 2026-11-08). Missing runtime → one sentence. | Every `dotnet test` machine has the SDK (owner confirmed). AOT refused (EnvDTE/`[ComImport]`); self-contained (~70 MB) and own Velopack package refused |
| L14 | PyTest moves into `source/RevitDevTool.PyTest/` with history; pixi tasks `lint`, `test`, `build`; golden JSON fixtures shared with the C# tests. | Contract drift becomes a CI failure |
| L15 | Changelog: one Keep a Changelog file per product (`source/DevTools.Daemon/CHANGELOG.md`, `CHANGELOG.md`, `source/DevTools.TestAdapter/CHANGELOG.md`, `source/RevitDevTool.PyTest/CHANGELOG.md`); root `Changelog.md` becomes an index. Draft from commits with `git-cliff` (via pixi) over the product's paths; a person edits; stable without a section fails; prerelease falls back to the draft. | Commits, not PR labels, are the source here |
| L16 | `build/` is the one release tool: `dotnet run --project build -- <target> [product]` with targets `ci`, `pack`, `release [--dry-run]`, `publish`, `channels`, `changelog`. Each product declared once (paths, version file, tag pattern, changelog, artifacts); CI path filters, bump warning, and changelog read that declaration. YAML holds triggers, permissions, environments, OIDC steps only. | TUnit/pyRevit pattern already used here |
| L17 | One trunk branch; `release/<product>/X.Y` only for hotfixes. Order across products: provider first (`daemon` if `minUpdaterVersion` moves, then `addin`, then `nettest`/`pytest`). | Trunk-based; 0055 §10 newer side explains |
| L18 | Keep it safe-enough, not over-engineered. | Owner's standing guidance |
| L19 | Trunk is `develop` (the default branch). `main` is retired; a release is marked by its tag. | Owner 2026-11-08: `develop` is the default, `main` only marked releases |
| L20 | First versions: `daemon` and `addin` `4.0.0-beta.8`, `nettest` `0.3.0`. | Owner 2026-11-08: continue the beta numbers so no two releases share a version; one beta round proves the new pipeline before `4.0.0`. `nettest` breaks (runner and framework runtimes in the package), and a `0.x` break raises `MINOR` |
| L21 | Bump rules for `0.x`, framework floor, and framework major are 0059 §5. | Owner 2026-11-08; 0060 §9 said "bump" without saying which part |
| L22 | P3b proves `release.yml` with `release nettest --dry-run`. The first real `nettest` publish comes after the first `addin` release; a `nettest` prerelease may pair with an `addin` prerelease. | Owner 2026-11-08: the new adapter stops with the update sentence on every shipped add-in (0060 §4 step 2) |
| L23 | Product paths for testing follow 0060 §1 (0059 §1): `DevTools.Testing.Host` in `addin`; framework runtimes, MTP providers, adapter, runner in `nettest`; `Testing.Abstractions`, `Testing.Transport`, `props/TestingRuntime.props` in both. | One owner per piece, so CI and the bump warning name the right products |
| L24 | No global version property: each entry project reads its product's file, shared libraries keep the SDK default, and `AppVersion` names the process's product (entry assembly, or the add-in assembly registered at add-in startup). Local packs are `0.0.0-local.<UTC stamp>+<sha>`; samples float on `0.0.0-local.*`. | Found 2026-11-08: `AppVersion` reads `DevTools.Utilities`, which three products share; NuGet ignores `+sha`, so `0.0.0-local+<sha>` would collide in the cache |
| L25 | Product paths come from the `ProjectReference` closure of each product's roots, written to `.github/product-paths.yml` by `build -- paths` and checked in every .NET CI job. | No hand list to drift; `dorny/paths-filter` reads a committed file on ubuntu |
| L26 | `nettest` and `pytest` GitHub Releases are created with `gh release create` in `release.yml`; `daemon` and `addin` publish through `build -- publish`. The PyPI publish job runs on ubuntu. | `pypa/gh-action-pypi-publish` is Docker-only; a two-asset release needs no .NET tool. Signing, asset order, and `MakeLatest` stay in `build/` |
| L27 | **Clean cut, no compatibility inside the repo either.** Each phase deletes what it replaces in the same PR: no aliases of old targets, no old workflow beside a new one, no fallback notes source. `build/` reads only `<product>/v*` tags; unprefixed tags, the PyTest repo tags, and versions already on NuGet/PyPI are history, not input. The first release of each product is the baseline; its version is still above what the registry serves (`0.3.0` > `0.2.x`, Q4 > `0.6.0`), so `dotnet restore` and `pip` pick it. `LegacyInnoInstall` (3.0.0 Inno cleanup) goes. The root `Changelog.md` is deleted; git history keeps it. | Owner 2026-11-08: "ko cần giữ compat cũ, có thể đổi 100% flow mới từ đây coi là base line cho các lần release sau" |

Rejected (with reason) — do not propose again:

- Lockstep version for all (TUnit style): republishes unchanged packages.
- One "distribution release" for Daemon + add-in with per-component versions and
  asset reuse: only needed for shipped Daemons (L1 removed that need).
- No GitHub Release for satellites: only needed while the Daemon read the list.
- `channels.json` on GitHub Pages: ties updates to the docs deploy.
- Four release workflows; Changesets; release-please; keeping GitVersion; PR-label
  release notes; SemVer in `pyproject.toml` converted at build.

## Open questions (owner decides; ask before the phase that needs it)

- Q1 Trunk name: answered, L19.
- Q2 Velopack update source: answered by P0 item 2. Velopack does not fall back; the
  Daemon's `VelopackReleaseSource` filters deltas (P5).
- Q3 First versions: answered, L20.
- Q4 First `pytest` version: answered, `0.6.1` (owner 2026-11-08).
- Q5 PyTest dev tool: answered, pixi only and `uv.lock` deleted (owner 2026-11-08).

## Scope

In scope: everything in 0059 §1–§11 and "What is removed".

Out of scope: the framework runtime move (plan 0060); McpGateway; changes to 0055 ranges.

## Approach

Phases are ordered so each lands green on its own. `nettest` goes first because it
is the smallest product and exercises the whole `release.yml` shape.

### Order and delegation

| Phase | Needs | Size | Suggested agent |
|---|---|---|---|
| P0 | owner OK for item 1 | small, exploratory | sonnet |
| P1 | — | large; everything else reads it | opus |
| P2 | P1, same PR (P1 removes the old `pack` that `Compile.yml` calls) | medium, YAML + one owner step | sonnet |
| P3b | P1 (P2 for CI on the PR) | medium | sonnet |
| P4 | P1, Q4, Q5 | medium; history import is one-shot, review before push | opus |
| P5 | P0 item 2 | large; engine behavior | opus |
| P6 | P1, P3b, P5 | large; first real releases | opus, owner present for publishes |
| P7 | P1, P0 item 4 | small | sonnet |
| P8 | P6 | small, docs and owner settings | sonnet |

P1 + P2 first, as one PR; then P3b, P4, P5, P7 can run in parallel worktrees; P6 joins
them; P8 last. Deletions happen in the phase that replaces the thing (L27), so P8 holds no
code removal.

### Facts the phases build on (read 2026-11-08)

- `build/`: `Program.cs:19-50` picks modules with `args.Contains` (`test`, `pack`,
  `publish`, `adapter`; no args = compile). Config: `appsettings.json`, user secrets,
  env (`Build__Version`, `Update__SigningKey`, `Update__RequireSignature`,
  `Update__MinUpdaterVersion`). `ResolveVersionModule` gives Version, Prefix, Suffix,
  IsPrerelease, PreviousVersion (`git describe --tags HEAD^`); 11 modules read it.
  `SolutionCompile:25-29` and `CompileDaemonModule:31-32` pass `VersionPrefix`/`VersionSuffix`
  as global properties. No csproj except the adapter has `<Version>`. No tests for `build/`.
- Versions in code: `DevTools.Utilities/AppVersion.cs` reads the informational version of
  the `DevTools.Utilities` assembly; `InstanceRequestHandler.cs:33` reports it as
  `productVersion`. Today one global version makes that right; with products it is not.
- Modules per future product: Daemon = `CompileDaemon`, `PackDaemon` (vpk, `vpk download github`
  for the delta, `--channel prerelease`), feed signing. Add-in = `CompileBase`/`CompileExpand`
  (every `Release.Autodesk.20xx` from `RevitDevTool.slnx`, one after another), `CreateBundle`,
  `PackPayloads`, `GenerateUpdateManifest` (`minUpdaterVersion` defaults to the release
  version), manifest signing. Both: `VerifyRelease`, `GenerateChangelog` (root
  `Changelog.md`, else GitHub generated notes), `PublishGithub` (draft, payloads, feeds last,
  undraft; no `make_latest`; deletes the tag on failure).
- `DevTools.Update`: `IReleaseProvider.GetLatestAsync(prerelease)` / `GetAsync(version)`,
  `GitHubReleaseProvider` (Octokit), `CachedReleaseProvider`, `Release(Version, Prerelease,
  Assets)`, `ReleaseReader` (signed reads, `.sig` beside the asset), `VelopackReleaseSource`
  (an `IUpdateSource` that reads `releases.<channel>.json` and entries from one `Release`).
  `UpdateEngine.RunCycleAsync` gets one release for both the Daemon and the payloads;
  `:176` compares the manifest version with the Daemon version in notify mode;
  `FindSetupReleaseAsync:411-419` looks for the release tagged with the Daemon version.
  Options: `Repository`, `GitHubToken`, `PublicKey` (Daemon `appsettings.json`).
  Tests: `FakeReleases` in `tests/DevTools.Update.Tests/TestSupport.cs`.
- Workflows: five files; no `setup-dotnet` (runner SDK, `global.json` 10.0.400); actions
  SHA-pinned except `PublishTestAdapter.yml`; Dependabot covers `github-actions` only;
  docs are Docusaurus + bun 1.4.2 in `docs/`; `TestReleaseInstall.yml` steps are the model
  for `verify-install`. Everything .NET needs Windows (`build` is `net10.0-windows`).
- PyTest repo: 44 commits on `main`, tags `v0.0.1`–`v0.6.0`, uv only (no pixi), hatchling,
  `pywin32` dependency, PyPI publisher environment `pypi`, 34 unit tests in
  `tests/plugin` (no host), live tests in `tests/{Revit,Civil3d,Revit_Ipy}`, dev-only
  `[tool.pytest.ini_options]` (host_name, host_version). Wire: `models.py` and
  `constants.py` (`PROTOCOL_CURRENT=1`, `MIN=1`) ↔ `PytestContracts.cs` and
  `PytestProtocol.cs`. Known drift: `engine` defaults to `""` in C#, `None` in Python.
- Samples take the adapter through the root `Directory.Packages.props:83`
  (`RevitDevTool.TestAdapter` `0.2.0`) from the local `output/nuget` feed.

### P0 Spikes (no product change; results go under Decisions)

1. **Tag with a slash.** One throwaway public prerelease `spike/v0.0.0-spike.1` with one
   asset: the download URL `releases/download/spike/v0.0.0-spike.1/a.txt` works without
   the API, and Octokit `NewRelease` with `MakeLatest = false` keeps `latest` unchanged.
   Delete it after. (Owner approves, because it is visible on the repo.)
2. **Velopack across releases (Q2).** Locally, `vpk pack` 0.0.1, 0.0.2, 0.0.3 of a tiny
   app, each into its own folder that holds only its own assets, the way a `daemon/v*`
   release will. Serve the folders over HTTP and update 0.0.1 → 0.0.3 through
   `VelopackReleaseSource` with a `Release` for 0.0.3. Record: does the 0.0.2 delta lookup
   fail and Velopack take the full package, or does it stop? Expected: full package, which
   is what 0059 accepts for a client more than one version behind. If it stops, the
   source lists only entries present in the release.
3. **Delta without `vpk download github`.** That command reads GitHub's latest release,
   which will be another product's. Pre-fill the vpk release folder with
   `gh release download <prev tag> -p "releases.<channel>.json" -p "*-full.nupkg"` and
   confirm `vpk pack` writes a delta.
4. **git-cliff per product.** `git-cliff --tag-pattern '^nettest/v' --include-path
   'source/DevTools.TestAdapter/**' --unreleased` on this repo gives a usable draft;
   record the commit-parser config that keeps `chore`, `ci`, `test`, `docs` out.

Done when each item has one line of result under Decisions and Q2 is answered.

### P1 `build/` products and versions

Goal: `dotnet run --project build -- <target> [product]` with every target of 0059 §10
except `publish` and `channels` (P3b, P5). The old argument handling (`args.Contains`
for `test`, `pack`, `publish`, `adapter`) is replaced, not aliased (L27).
`PublishRelease.yml`, `PublishTestAdapter.yml`, and `GitVersion.yml` are deleted here;
`Compile.yml` is replaced by `ci.yml` in P2, which lands in the same PR.

**Product declaration** (`build/Products/Product.cs`, one record, four static instances):

| Field | daemon | addin | nettest | pytest |
|---|---|---|---|---|
| Roots | `source/DevTools.Daemon/DevTools.Daemon.csproj` | `source/RevitDevTool/RevitDevTool.csproj`, `source/AcadDevTool/ACadDevTool.csproj` | `source/DevTools.TestAdapter/DevTools.TestAdapter.csproj`, `source/DevTools.TestRunner/…csproj`, `source/DevTools.{NUnit,TUnit,MSTest}.{MTP,Runtime}/…csproj` | — |
| Extra paths | — | `props/PackageContents.xml`, `source/RevitDevTool.PyTest/tests/golden/**` | `props/TestingRuntime.props` | `source/RevitDevTool.PyTest/**` |
| Version file | `<Version>` in the Daemon csproj | `<Version>` in `props/AddinVersion.props` | `<Version>` in the adapter csproj | `version` in `source/RevitDevTool.PyTest/pyproject.toml` |
| Tag pattern | `daemon/v*` | `addin/v*` | `nettest/v*` | `pytest/v*` |
| Changelog | `source/DevTools.Daemon/CHANGELOG.md` | `CHANGELOG.md` | `source/DevTools.TestAdapter/CHANGELOG.md` | `source/RevitDevTool.PyTest/CHANGELOG.md` |
| Grammar | SemVer | SemVer | SemVer | PEP 440 subset |

- **Paths are computed, not listed.** For a .NET product: the folders of the roots and
  of their `ProjectReference` closure (read csproj XML recursively), plus every
  `tests/*` project that references a project in that closure, plus the files every
  .NET build reads (`Directory.Build.props`, `Directory.Build.targets`,
  `Directory.Packages.props`, `global.json`, `dotnet-tools.json`, `RevitDevTool.slnx`,
  `props/**`, `build/**`), plus the extra paths. A shared project lands in each product
  whose closure has it, which is L23 without a hand list.
- `build -- paths` writes `.github/product-paths.yml` (the `dorny/paths-filter` format, one
  key per product, plus `docs` and `workflows`). `build -- paths --check` fails when the
  committed file differs; every .NET CI job runs it. Adding a `ProjectReference` edits a
  csproj that is already in a product, so the stale file is always caught by a job that runs.
- Test projects of a product: the same rule, so a shared test project (for example
  `DevTools.Utilities.Tests`) runs in each product that ships its project.
- Contract files per product, for the `Min` warning: the files that construct a
  `ProtocolRange` or hold a `Min` (find with `ProtocolRange(` and `TestingRuntime.props`;
  today `PytestProtocol.cs`, `props/TestingRuntime.props`, the testing protocol in
  `DevTools.Testing.Transport`, the Ipc ranges, `constants.py`).

**Versions:**

- Readers: MSBuild-style `<Version>` (first non-empty element) and the `version` line of
  `pyproject.toml`. Grammar per L8, as two regexes in one place, each with unit tests.
- `props/AddinVersion.props` holds `<Version>4.0.0-beta.8</Version>` and
  `<MinUpdaterVersion>4.0.0-beta.8</MinUpdaterVersion>`; imported by `RevitDevTool.csproj`
  and `ACadDevTool.csproj`. The Daemon csproj gets `<Version>4.0.0-beta.8</Version>`.
  `GenerateUpdateManifestModule` reads `MinUpdaterVersion` from the props, not from env;
  `UpdateOptions.MinUpdaterVersion` goes.
- No global version property any more: `SolutionCompile` and `CompileDaemonModule` stop
  passing `VersionPrefix`/`VersionSuffix`. Each entry project takes its version from its
  file; shared libraries keep the SDK default. A `pack` or `release` passes `-p:Version`
  only to its product's entry projects (the adapter pack already does this and keeps it).
- `AppVersion.Current` must name the product of the process, not of `DevTools.Utilities`
  (L24): it reads the entry assembly when that is ours (Daemon, TestRunner) and, in a host,
  the add-in assembly that the add-in's startup registers once. Test: a host-side test
  sets the add-in assembly and `instance/info` reports its version.
- Non-release versions (L9, refined by L24): CI `0.0.0-ci.<run>+<sha>`, local
  `0.0.0-local.<UTC yyyyMMddHHmmss>+<sha>`, pytest `0.0.0.dev<run>`. NuGet ignores `+…`, so
  the time stamp keeps local packs distinct. Samples float on `0.0.0-local.*`
  (`CentralPackageFloatingVersionsEnabled` in the samples scope only), so a local
  `pack nettest` is picked up without clearing the cache; `RemoveLastExtraction` in
  `PackTestAdapterModule` goes.
- Remove GitVersion: `GitVersion.yml`, the GitVersion branch of `ResolveVersionModule`,
  `BuildOptions.Version` (`Build__Version`). `ResolveVersionModule` becomes
  `ProductVersion` per product: version from the file (or the `0.0.0` form for `pack`),
  previous version = highest tag of the product pattern (`git tag --list '<product>/v*'`,
  compared by version, not by date), none for the first release. Nothing else is read:
  no unprefixed tag, no registry query (L27). A version the registry already holds is
  refused by the registry itself at push.
- `AdapterArchitectureTests` `GitVersion()` assertion goes with GitVersion.

**Targets** (`Program.cs` reads `args[0]` as the target and `args[1]` as the product; an
unknown target or product prints the list and exits 1):

| Target | P1 does |
|---|---|
| `paths [--check]` | Write or check `.github/product-paths.yml` |
| `ci <product> [--base <sha>]` | `paths --check`, compile the product (addin: every `Release.Autodesk.*` config, as `CompileBase`/`CompileExpand` do; daemon: Daemon Release; nettest: the roots), run its test projects (`dotnet test` per project, `--no-build`), then the warnings below |
| `pack <product>` | Artifacts at the `0.0.0` version into `output/<product>/`: daemon `Setup.exe` + feed (no delta, no signing); addin payload zips + manifest + bundle; nettest nupkg; pytest wheel (build in a temp copy with the version line replaced) |
| `release <product> [--dry-run]` | Validate (below); without `--dry-run` also run `ci` and the release artifacts at the file version |
| `changelog <product>` | P7 |

Warnings from `ci` (GitHub `::warning::` lines, never a failure), against `--base`
(PR base or the push's `before`): the product's paths changed and its version file did not;
a commit with `!` or `BREAKING CHANGE` touches the product and the next version does not raise
`MAJOR` (`MINOR` under `0.x`); a `feat` commit touches the product after an `rc` tag of its
current version.

`release <product>` refuses, each with one sentence naming the fix:
- `0.0.0` versions, a version that fails the grammar, or one not greater than the
  product's latest tag;
- a label that goes back (`rc` → `beta`) for the same `MAJOR.MINOR.PATCH`;
- a stable version without a `## [X.Y.Z]` section in the product changelog;
- `addin` stable with a prerelease `MinUpdaterVersion`, or with a non-empty
  `PublicAPI.Unshipped.txt` (the bump PR moves the lines, 0059 §6);
- and warns when a contract file changed since the previous tag and `MAJOR` did not rise.
`--dry-run` prints version, previous version, tag, prerelease flag, notes source, and the
artifact list, and writes them to `$GITHUB_OUTPUT` and the step summary when present.

**Tests:** new `tests/Build.Tests` (MSTest.Sdk, references `build/`): grammar (both
regexes), label order, previous-tag pick (other products' and unprefixed tags ignored), path closure on a
fixture repo of three csproj files, `product-paths.yml` writer, each refusal sentence.

**Proof:** `dotnet run --project tests/Build.Tests`; `dotnet run --project build -- paths
--check`; `ci nettest` and `ci daemon` locally; `pack nettest` then a sample restore picks the
`0.0.0-local.*` package; `release addin --dry-run` prints `addin/v4.0.0-beta.8` with no
previous version; `rg -i gitversion` finds nothing outside docs history.

### P2 `ci.yml`

`.github/workflows/ci.yml` replaces `Compile.yml` and `Docs.yml`; both are deleted in this
phase:

```yaml
on:
  pull_request: { branches: [develop] }
  push: { branches: [develop] }
  merge_group:
  workflow_dispatch:
permissions: {}
concurrency: { group: ci-${{ github.ref }}, cancel-in-progress: ${{ github.event_name == 'pull_request' }} }
```

| Job | Runner | Permissions | Steps |
|---|---|---|---|
| `changes` | ubuntu-latest | `contents: read`, `pull-requests: read` | checkout; `dorny/paths-filter` with `filters: .github/product-paths.yml`; outputs one boolean per key. `workflow_dispatch` sets all true |
| `daemon` | windows-2025 | `contents: read` | checkout `fetch-depth: 0`; NuGet cache (key as in `Compile.yml`); `dotnet run --project build -- ci daemon --base <base>`; on push: `pack daemon`, upload `output/daemon` (3 days) |
| `addin` | windows-2025 | `contents: read` | same with `ci addin`; on push: `pack addin`, upload payloads + manifest + bundle (3 days) |
| `nettest` | windows-2025 | `contents: read` | `ci nettest`; `pack nettest`; upload the nupkg (3 days) |
| `pytest` | windows-2025 (`pywin32`) | `contents: read` | `prefix-dev/setup-pixi` with `manifest-path: source/RevitDevTool.PyTest/pixi.toml`, `pixi run lint`, `pixi run test`, `pixi run build`. Added in P4; until then the key exists and the job is skipped |
| `docs` | ubuntu-latest | `contents: read` | the `Docs.yml` build steps (bun 1.4.2, frozen lockfile, typecheck, build); on push: upload the Pages artifact |
| `docs-deploy` | ubuntu-latest | `pages: write`, `id-token: write`, `environment: github-pages` | needs `docs`, only on push to `develop` |
| `workflows` | ubuntu-latest | `contents: read` | `actionlint`; `zizmor` (offline, fail on high) |
| `gate` | ubuntu-latest | none | `if: always()`, needs all of the above; `re-actors/alls-green` with `allowed-skips` = every job except `changes` |

- `<base>` is `github.event.pull_request.base.sha`, or `github.event.before` on push;
  `ci` prints the warnings (P1).
- The NuGet cache key also hashes `dotnet-tools.json`. No `setup-dotnet`: the image SDK
  satisfies `global.json` today; the `ci` target fails with one sentence if not.
- Every action pinned by SHA with the tag in a comment (zizmor checks it). Diagnostics on
  failure (`*.binlog`, hang dumps) uploaded as in `Compile.yml`, 7 days.
- `.github/dependabot.yml`: `github-actions` (as now), `nuget` (directory `/`, one group
  per weekly run, `Microsoft.Testing.*`/`MSTest*`/`NUnit*`/`TUnit*` ignored because 0060
  moves them only by hand), `bun` for `/docs`; all weekly, label `dependencies`.
- Delete `Compile.yml` and `Docs.yml`; their useful steps (NuGet cache key, diagnostics
  upload, the docs build) are in the jobs above.

Owner: make `ci / gate` the only required check on `develop` (0059 L12), after one green run.

Proof: a PR that touches only `docs/` runs `changes`, `docs`, `gate`; one that touches
`source/DevTools.TestRunner` runs `nettest` and no other product job; a PR with a stale `product-paths.yml` fails the product job;
`actionlint` and `zizmor` pass on the new file.

### P3a TestRunner in the nupkg (started 2026-11-07, independent of P1 and P2)

Pulled forward because the first `nettest` release must carry the runner and the
0060 framework runtimes together, and the 0060 follow-up plan
([2026-11-07](../completed/2026-11-07-testadapter-build-cleanup-and-compat-hardening.md)) left the
adapter version bump waiting on it. Samples already consume the adapter as a
`PackageReference` from the local `output/nuget` feed, so a runner inside the package
also serves in-repo development.

Expected end state (L13):

- `DevTools.TestRunner.csproj`: `net10.0-windows`, framework-dependent single-file,
  `RollForward=Major`. The `VelopackPublish` branch and the `DeployDevToolsTestRunner`
  copy to the bundle `Contents` folder go; the runner is reached only through the
  package.
- `DevTools.TestAdapter.csproj`: `dotnet pack` alone yields a complete package. A pack
  target publishes the runner once (not per TFM) and places the single-file exe at
  `tools/runner/DevTools.TestRunner.exe`; pack fails with one sentence when the exe
  is missing.
- `build/RevitDevTool.TestAdapter.targets`: `devtools.runnerPath` metadata always
  names the runner inside the package (`$(MSBuildThisFileDirectory)..\tools\runner\...`).
  The MSBuild property `TestingRunnerPath`, the environment variable, and the
  `testconfig.json` override are gone (owner, 2026-11-08): the loader reads
  `runnerPath` from the metadata only. A dev-built runner is tested by packing it
  (`build -- adapter`) and restoring the samples.
- `TestingRunnerPaths.ResolveRunnerPath` no longer looks in the Daemon folder
  (`AppUtils.GetTestRunnerExePath` is removed); an unreachable path is one sentence
  naming it.
- Missing .NET runtime: the apphost exits before the runner's own code runs, so
  `ProcessTestRunnerClient` maps that failure to one sentence, "RevitDevTool.TestAdapter
  needs the .NET <major> runtime or later." The major comes from the runner's target
  framework through one build constant, not a literal in a message.
- `build/`: `PackDaemonModule` publishes the Daemon alone; `MergeSharedRuntime` and
  the runner publish there go. `CompileTestRunnerModule` goes or shrinks to a compile.
  `PackTestAdapterModule` keeps restore + pack.
- `.github/workflows/TestReleaseInstall.yml`: the two runner checks go.
- `LegacyInnoInstall.Executables` keeps the runner name: it cleans old installs (the class
  itself goes in P6, L27).
- Tests: `PackageConsumerTests` asserts `tools/runner/DevTools.TestRunner.exe` in the
  nupkg and `devtools.runnerPath` metadata pointing into the package;
  `AdapterArchitectureTests` covers the targets default; `RunnerAssemblyBoundaryTests`
  no longer expects the bundle copy target; a Transport test covers the
  missing-runtime sentence; a loader test covers the metadata path.
- Docs, one layer each: `docs/architecture/Testing/README.md` (where the runner lives),
  `docs/product/host-testing.md` (requirement: .NET 10 runtime or
  later on the machine running `dotnet test`),
  `docs/agents/host-testing.md` and `docs/agents/verification.md` (dev lookup), the
  `revit-test` skill project-setup reference. ADR 0059 §3 already states the decision.

Proof: `tests/DevTools.TestAdapter.Tests` (package consumer on three TFMs),
`tests/DevTools.TestRunner.Tests`, `tests/DevTools.Testing.Transport.Tests`,
`dotnet build build/`, and one pack through the build module per the build skill.
A live `dotnet test` on a sample against the packed adapter proves the runner
launches from the package; it stays open until a host is available.

P3a result, 2026-11-07 (committed in `d075513b`):

| Proof | Result |
|---|---|
| `dotnet build build/` | 0 warnings, 0 errors; `PackDaemonModule` compiles without the runner |
| `tests/DevTools.Testing.Transport.Tests` | 66 / 66 |
| `tests/DevTools.TestRunner.Tests` | 34 / 34 |
| `tests/DevTools.Utilities.Tests` | 19 / 19 |
| `tests/DevTools.TestAdapter.Tests` (packs on three TFMs) | 96 / 96 |
| `dotnet pack source/DevTools.TestAdapter -c Release` | one `tools/` entry, `tools/runner/DevTools.TestRunner.exe`, 3.3 MB; `--version` exits 0 |
| `dotnet run --project build -- adapter` | `output/nuget/RevitDevTool.TestAdapter.0.2.0.nupkg`, 55 entries, runner included |
| Missing runtime | apphost exit `0x80008083` (no .NET) and `0x80008096` (framework missing) both map to the one sentence; codes verified against dotnet/runtime `host-error-codes.md` |

How it landed: `props/TestingRuntime.props` holds the runner's .NET major once; the runner
csproj and a generated Transport constant read it. `PackRunner` runs through
`BeforePack` in the outer build, restores and publishes the runner with only
`Configuration` passed down (other properties would rebuild the shared references into
the same `bin`/`obj`), and adds the exe as `None Pack`. `TestingRunnerPaths` no longer
reads the install folder; `AppUtils.GetTestRunnerExePath` is gone.
`CompileTestRunnerModule` is deleted; `MergeSharedRuntime` is gone from the Daemon pack.

Open after P3a:

- Resolved: `PackTestAdapterModule` passes the csproj `<Version>` as a global `-p:Version` to restore and pack, so the runner reports it; a direct `dotnet pack` without `-p:Version` still yields `1.0.0`, which is fine for tests.
- Dev machines keep a stale `DevTools.TestRunner.exe` in `%LOCALAPPDATA%\RevitDevTool\current`
  and the bundle `Contents`; the adapter no longer reads either (proven 2026-11-08:
  with the package runner missing the adapter stopped instead of falling back).
- Resolved 2026-11-08: live `dotnet test` for NUnit, TUnit, MSTest on Revit 2024,
  2025, 2027 against the packed adapter, one process per year; result table in the
  completed plan `2026-11-07-testadapter-build-cleanup-and-compat-hardening`.
- `dotnet run --project build -- adapter` deletes the restored package folder in the
  NuGet cache; a sample run in progress on another shell then fails with the
  missing-runner sentence until it restores again. Known, by design of the local
  feed (L9 removes it with `0.0.0` packs).
- Resolved 2026-11-08: the runner path has no override (environment variable and
  `testconfig.json` key removed), so the missing-runner sentence has one fix: restore
  the package again.

### P3b `release.yml` with `nettest`

`.github/workflows/release.yml`, the whole skeleton now, with the `daemon`/`addin`
branches failing with "product <x> is not enabled yet" until P6:

```yaml
on:
  workflow_dispatch:
    inputs:
      product: { type: choice, options: [daemon, addin, nettest, pytest], required: true }
run-name: release ${{ inputs.product }}
permissions: {}
concurrency: { group: release-${{ inputs.product }}, cancel-in-progress: false }
```

| Job | Runner | Env / permissions | Does |
|---|---|---|---|
| `validate` | windows-2025 | `contents: read` | `if: github.ref == 'refs/heads/develop' \|\| startsWith(github.ref, 'refs/heads/release/')`; checkout `fetch-depth: 0` + tags; `setup-pixi` (environment `release`, for git-cliff); `build -- release <product> --dry-run`; outputs `version`, `tag`, `prerelease`, `latest` |
| `build` | windows-2025 | `contents: read`, no secrets | `build -- release <product>`: tests, then artifacts at the file version, plus `notes.md`; upload artifact `release-<product>` (7 days) |
| `publish` | per product (below) | `environment: <product>`; `contents: write`, `id-token: write`, `attestations: write` | download the artifact; `actions/attest-build-provenance` on it; push to the registry, skipping a version it already has; GitHub Release and tag |
| `channels` | windows-2025 | P5 | `daemon`, `addin` only |
| `verify-install` | windows-2025 | P6 | `daemon`, `addin` only |

`publish` per product:

- `nettest` (windows-2025): `NuGet/login` (OIDC, user from `vars.NUGET_USER`), then
  `dotnet nuget push *.nupkg --skip-duplicate`; then the GitHub Release with `gh` (L26):
  `gh release view <tag>` exits 0 → skip; else `gh release create <tag> --target <sha>
  --title "RevitDevTool.TestAdapter <v>" --notes-file notes.md --latest=false
  [--prerelease] <nupkg>`.
- `pytest` (ubuntu-latest, the PyPI action is Docker-only): `pypa/gh-action-pypi-publish`
  with `skip-existing: true`, then the same `gh release create` with the wheel and sdist.
- `daemon`, `addin`: `build -- publish <product>` (P6), the only place a signing key is used.

0059 §7 says the job that runs repo code holds no secret. In this layout the tests and the
compile run in `build`, which has no secret; `publish` runs only `build -- publish` from the
same commit, in a protected environment. That wording is corrected in 0059 §7.

`nettest` content for its first release:

- `<Version>0.3.0</Version>` (L20).
- `source/DevTools.TestAdapter/CHANGELOG.md`, Keep a Changelog, hand-written `## [0.3.0]`:
  runner inside the package; framework runtimes from the add-in (0060); the framework
  versions they were built against (the table in the adapter README); needs the RevitDevTool
  `4.0.0-beta.8` add-in or later (the update sentence otherwise); `.NET 10` runtime on the
  machine that runs `dotnet test`. `PackageReleaseNotes` in the csproj links the file.
- Docs that pin `0.2.0` (`.agents/skills/revit-test/SKILL.md`,
  `references/project-setup.md`, `source/DevTools.TestAdapter/README.md`) move to `0.3.0` in
  the release PR.

P3b ends with `release nettest --dry-run` green in Actions on `develop`; the first real
publish waits for `addin` (L22).

Owner: environments `daemon`, `addin`, `nettest`, `pytest` (each with a required reviewer;
`daemon` and `addin` hold `UPDATE_SIGNING_KEY`), variable `NUGET_USER`, the nuget.org trusted
publishing policy (repo `trgiangv/RevitDevTool`, workflow `release.yml`, environment `nettest`).

### P4 PyTest move + `pytest` product

1. **Import with history.** In the PyTest repo first: drop tracked IDE and cache folders
   (`.idea`, `.run`, `.vscode`, `.pytest_cache`) if tracked; commit. Then in this repo:
   `git subtree add --prefix=source/RevitDevTool.PyTest <PyTest repo path> main`
   (44 commits, one merge commit). Old `v0.x` tags stay in the old repo as history (L27).
2. **Repo shape inside the folder.**
   - Delete `CLAUDE.md`; cut `AGENTS.md` to a pointer to `docs/agents/mcp-pytest-bridge.md`
     and the `revit-pytest` skill.
   - `pyproject.toml`: `project.urls` → `github.com/trgiangv/RevitDevTool` (Repository,
     Issues), `Changelog` → `blob/develop/source/RevitDevTool.PyTest/CHANGELOG.md`. Remove
     `[tool.pytest.ini_options]` (dev host settings) and add `[tool.ruff]` (from
     `tests/Revit_Ipy/pyproject.toml` ignores).
   - Dev host settings move to `tests/live.ini` (`testpaths`, `host_name`, `host_version`,
     timeouts, the commented Civil 3D variants); live runs use `pixi run live` (`pytest -c
     tests/live.ini`).
   - `uv.lock` is deleted; `pixi.lock` replaces it (Q5). Users still `pip`/`uv install` the
     package; only the repo's dev tool changes.
3. **pixi** (`source/RevitDevTool.PyTest/pixi.toml`, its own workspace, `win-64`, because
   of `pywin32`): conda deps `python 3.13.*`, `ruff`, `python-build`, `hatchling`;
   pypi deps `revitdevtool_pytest = { path = ".", editable = true }`. Tasks:
   `lint = "ruff check src tests/plugin && ruff format --check src tests/plugin"`,
   `test = "pytest tests/plugin tests/golden"`, `build = "python -m build --outdir dist"`,
   `live = "pytest -c tests/live.ini"`. Lowest supported Python stays `>=3.10` in the
   package metadata; CI tests on 3.13 only (safe-enough).
4. **Golden fixtures** in `source/RevitDevTool.PyTest/tests/golden/` (path declared in the
   `addin` and `pytest` products, P1): `run_request.json`, `run_response.json` (results,
   collection errors, `engine` set), `progress_notification.json`,
   `instance_info.json` (`productVersion`, `protocols.pytest`, `sender`).
   - Python: `tests/golden/test_golden.py` loads each file into the `models.py` type,
     serializes it, and compares as parsed JSON.
   - C#: `tests/DevTools.Execution.Pytest.Tests` links the folder (`<None Include=… Link=…
     CopyToOutputDirectory>`), `GoldenWireTests` deserializes into `PytestContracts`
     types and compares the same way.
   - The `engine` default drift (`""` vs `None`) is recorded, not changed: the fixtures set
     `engine`; one Python test documents the missing-field behavior.
5. **Release.** `pytest` enabled in `release.yml` (P3b shape); `ci.yml` `pytest` job on.
   `CHANGELOG.md` keeps its history; the first section from this repo is
   `## [<Q4 version>]` "Moved into the RevitDevTool repository; no behavior change".
6. **Docs and skills:** `docs/agents/mcp-pytest-bridge.md` (contracts and golden fixtures
   in one repo; bump `PytestProtocol` and `constants.py` in the same PR),
   `.agents/skills/revit-pytest/SKILL.md` (path `source/RevitDevTool.PyTest`, `pixi run
   live`), `AGENTS.md` repo anchors, root `.gitignore` (`source/RevitDevTool.PyTest/.pixi`,
   `dist`).

Owner: environment `pytest`; PyPI trusted publisher (repo `trgiangv/RevitDevTool`, workflow
`release.yml`, environment `pytest`); after the first publish from here, remove the old
publisher (`pypi` environment of the old repo) and archive the old repo with a README pointer.

Proof: `git log --oneline -- source/RevitDevTool.PyTest | measure` ≥ 44; `pixi run lint`,
`test`, `build` green; C# `GoldenWireTests` green; a deliberate field rename in one fixture
fails both sides; `release pytest --dry-run` prints `pytest/v<Q4>`.

### P5 `channels.json` and the update engine

**Model** (`DevTools.Update/Releases/ChannelIndex.cs`, camelCase in `UpdateJsonContext`):

```text
ChannelIndex { schema: 1, stable: Channel, prerelease: Channel }
Channel      { daemon: { version, feed }, addin: { version, manifest } }
```

`feed` is the release download folder (`…/releases/download/daemon/v4.0.0-beta.8/`);
`manifest` is the full URL of `update-manifest.json`. Schema other than 1 is an
`UpdateIntegrityException`.

**Release becomes a folder.** `Release(Version, Prerelease, Uri BaseUrl)`, and
`Asset(name)` = `BaseUrl` + name. `HasManifest` and the asset dictionary go: an `addin`
release always has a manifest, and a missing one is an integrity error from the download.
`ReleaseReader` and `VelopackReleaseSource` keep their code paths.

**Provider.** `IReleaseProvider` is replaced by `IChannelIndexSource.GetAsync(ct)`:
- `ChannelIndexSource` reads `channels.json` and `channels.json.sig` through `ReleaseReader`
  from `UpdateEngineOptions.ChannelIndexUrl` (default
  `https://github.com/trgiangv/RevitDevTool/releases/download/channels/channels.json`), one
  request plus the signature, no REST API.
- `CachedReleaseProvider` becomes the cache of the index (1 minute, as now).
- `ChannelIndex.Pick(prerelease)` returns `(Release daemon, Release addin)`; the
  `prerelease` side is already "newest of both" when written (P5 writer), so the client does
  no merging.
- Gone: `GitHubReleaseProvider`, `IReleaseProvider.GetAsync(version)`, `Repository`,
  `GitHubToken`, the Octokit reference of `DevTools.Update` (the build keeps Octokit through
  ModularPipelines), `Update__GitHubToken` in the install test.

**Engine** (`UpdateEngine`):
- `RunCycleAsync`: pick by `policy.IsPrerelease`; read the signed manifest from the `addin`
  release; outdated payloads compare the `addin` version per host version (as now,
  `:189-193`); `minUpdaterVersion` against the installed Daemon (as now); the Daemon updates
  when the `daemon` version is newer than the installed one, through `VelopackReleaseSource`
  on the `daemon` release.
- `VelopackReleaseSource.GetReleaseFeed` drops every `Delta` entry unless the feed has a
  `Full` entry whose version equals `latestLocalRelease.Version` (P0 item 2). Tests: an
  installed version one behind keeps the delta; two behind gets only the full entry.
- Notify mode (`:176`) offers the two parts separately: add-in when its version is newer
  than the installed payloads, Daemon when its version is newer than the Daemon.
- `SkippedVersion` holds an add-in version only; Daemon updates are not skippable (they
  apply on exit, 0052). The Skip UI itself stays in the auto-update plan.
- `FindSetupReleaseAsync` (`--select`, first run): use the `addin` release of the install's
  channel; no lookup by the Daemon's version.
- The Updates tab shows the Daemon version and the add-in version per host version (0059
  Consequences: support asks for both).

**Writer** (`build -- channels <product> --version <v>`; P6 wires it into `release.yml`):
download the current `channels.json` from the `channels` release (absent → empty index);
set `stable.<product>` when `<v>` is stable; set `prerelease.<product>` to the higher of the
new value and `stable.<product>`; for the other product, also lift `prerelease` to its stable
when stable is higher; validate that every URL answers `HEAD` 200; sign with
`Update__SigningKey`; `gh release upload channels channels.json channels.json.sig --clobber`
(create the `channels` release first if missing, `--latest=false`, title "Update channels").
Upload order is the JSON first, then the signature; a reader that meets the pair mid-swap
fails the signature check and retries on the next cycle (the scheduler backs off), which is
accepted.

**Tests** (`tests/DevTools.Update.Tests`): `FakeReleases` becomes `FakeChannels` (serves an
index and release folders); cases: stable install ignores prerelease; prerelease install
takes stable when newer; Daemon-only change downloads no payload; add-in-only change does
not touch the Daemon; bad index signature stops the cycle with no change; schema 2 refused;
`minUpdaterVersion` above the Daemon holds payloads; first-run selection uses the channel's
add-in. Writer tests in `tests/Build.Tests`: the four channel transitions, including a
stable that overtakes an open prerelease.

**Proof:** both test projects green; a local run of the Daemon against a file index served
from a folder (`ChannelIndexUrl` pointed at `http://localhost`) installs and updates one
year with a signed index and refuses an unsigned one.

### P6 `daemon` and `addin` products

**Assets per release:**

| Product | Assets |
|---|---|
| `daemon` | `RevitDevTool-Setup.exe`, `RevitDevTool-<v>-full.nupkg`, `RevitDevTool-<v>-delta.nupkg` (when a previous release of the channel exists), `releases.<channel>.json` + `.sig`, `assets.<channel>.json`, `RELEASES*` |
| `addin` | `RevitDevTool-<v>-<year>.zip` per year, `update-manifest.json` + `.sig`, `RevitDevTool.bundle.zip` |

**build/** (`publish <product>` and the `release` artifacts):
- `daemon`: `CompileDaemon` → `PackDaemon` → sign `releases.*.json` → `VerifyRelease`
  (Daemon part). The delta base comes from `gh release download <previous daemon tag of the
  same Velopack channel> -p "releases.<channel>.json" -p "*-full.nupkg"` into the vpk
  release folder (P0 item 3); `vpk download github` goes. No previous tag → no delta.
- `addin`: compile every year → `CreateBundle` → `PackPayloads` → `GenerateUpdateManifest`
  (`version` = add-in version, `minUpdaterVersion` from `AddinVersion.props`, `notesUrl` =
  `…/releases/tag/addin/v<v>`, `channel`) → sign the manifest → `VerifyRelease` (add-in
  part). `release addin` also checks that a `daemon` release ≥ `minUpdaterVersion` exists in
  the same channel class (stable needs stable).
- `PublishGithubModule` takes the product: tag `<product>/v<v>` on the validated commit, name
  "RevitDevTool Daemon <v>" / "RevitDevTool add-in <v>", body = `notes.md`, `Prerelease`,
  `MakeLatest` = true only for a stable `daemon`, false otherwise; draft → payloads → feeds
  last → undraft, as now; on failure the draft and tag are deleted, as now. Skips when the
  tag already has a published release.
- Signing key stays `Update__SigningKey` from the environment secret; `Update__RequireSignature`
  is always true in `publish`.
- Deleted here (L27): `TestReleaseInstall.yml` (now `verify-install`), `vpk download github`,
  `LegacyInnoInstall.cs` and its call in `DevTools.Daemon/Program.cs:33`, the old
  `PublishGithubModule` behavior without a product, and `UpdateOptions` fields nothing reads.

**release.yml** (enable `daemon` and `addin`):
- `publish` (windows-2025, environment `<product>`): `build -- publish <product>` with the key.
- `channels` (needs `publish`, environment `<product>` for the key, concurrency group
  `channels`, `contents: write`): `build -- channels <product> --version <v>` (P5 writer).
- `verify-install` (needs `channels`, clean windows-2025, `contents: read`, `issues: write`,
  timeout 30 min), the `TestReleaseInstall.yml` steps with three changes: Setup comes from the
  `daemon` entry of `channels.json` for the release's channel; the payload version expected in
  `installation.json` is the `addin` entry; no `Update__GitHubToken`. Keeps: `--silent` install,
  `--select "Revit 2025" --select "AutoCAD 2025"`, PackageContents and file checks, no Daemon
  in `Contents`, `--stdio` exits 0 within 60 s, issue on failure.

**Release proof** (taken over from the auto-update plan), on the first betas:
1. `daemon/v4.0.0-beta.8`, then `addin/v4.0.0-beta.8`: `verify-install` green; clean install
   on Windows Sandbox: Setup, Updates tab shows both versions, Revit and AutoCAD load, MCP
   snippet path, `dotnet test` with the adapter from P3b.
2. `daemon/v4.0.0-beta.9` alone: the Daemon self-updates by delta and no payload is downloaded.
   A Daemon still on an install two versions back (keep one from before step 1's update, or
   install beta.8 again after beta.10) takes the full package and its files match a fresh install.
3. `addin/v4.0.0-beta.9` alone with Revit open: the payload goes pending, then applies after
   Revit closes; notify mode offers it instead.
4. `4.0.0` stable for both after the beta proof (Release order step 5).

Proof for the phase: `tests/Build.Tests` (asset lists per product, `MakeLatest` rule,
`minUpdaterVersion` check), `release daemon --dry-run` and `release addin --dry-run` in
Actions, then the release proof above.

### P7 Changelog targets

- **git-cliff through pixi:** root `pixi.toml` gains feature `release` (`git-cliff` from
  conda-forge) and environment `release = { features = ["release"], no-default-feature = true }`,
  so the host Python environment is untouched. `build/` runs `pixi run -e release git-cliff …`.
- **One `cliff.toml`** at the root (P0 item 4 config): Keep a Changelog sections;
  `feat` → Added, `fix` → Fixed, `perf`/`refactor` → Changed, `!` or `BREAKING CHANGE` →
  a "Breaking" line at the top of Changed; `chore`, `ci`, `test`, `docs`, `build`, `style`
  skipped; merge commits skipped.
- **`build -- changelog <product>`:** `git-cliff --config cliff.toml --tag-pattern
  '^<product>/v' --include-path <each product path> --unreleased --strip all`; the result
  replaces the `## [Unreleased]` block of the product changelog (inserted under the title when
  missing); other sections untouched.
- **Notes for a release** (`release <product>` writes `notes.md`): the `## [<v>]` section when
  present; else, for a prerelease, the same git-cliff draft for the range since the previous
  product tag (the first release of a product has a hand-written section); stable without the
  section is refused (P1). `GenerateChangelogModule` and `.github/release.yml` (GitHub
  generated notes) are deleted here. `nettest` also sets
  `PackageReleaseNotes` to a link to its changelog; `pytest` keeps `project.urls.Changelog`.
- **Seeding:** the first section of each product is hand-written in its release PR
  (`daemon` and `addin` `4.0.0-beta.8`, `nettest` `0.3.0`, `pytest` Q4); it describes the
  release as it stands, not a diff from the old scheme. The root `Changelog.md` is deleted
  (L27); the root README links the four product changelogs.

Proof: `build -- changelog nettest` on `develop` writes an `Unreleased` block with only
adapter-path commits and no `chore`/`docs`; `tests/Build.Tests` covers the block replacement.

### P8 Cleanup and docs

After P6. No code removal is left for this phase: each earlier phase deleted what it
replaced (L27). Check with `rg -i "gitversion|PublishRelease|PublishTestAdapter|Compile\.yml|TestReleaseInstall|Build__Version|LegacyInno"`
outside `docs/decisions/` and `docs/plans/completed/`: no hit.

- Update `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  `docs/agents/verification.md`, and the product/architecture pages that describe
  update discovery (0048 text) — one layer each, per `AGENTS.md`.
- Pages and comments that name the old workflows or GitVersion (each phase may already
  fix the ones it touches): `docs/agents/build-matrix.md`, `.agents/skills/platform-change/SKILL.md`,
  `docs/architecture/Testing/README.md` (packaging table and bump line),
  `docs/product/host-testing.md` (packaging list), the comments in
  `DevTools.TestAdapter.csproj`, `PackTestAdapterModule.cs`, `UpdateOptions.cs`.
- `docs/decisions/README.md` and the product page for updates: discovery through
  `channels.json`, two versions shown in the Updates tab.
- `docs/agents/mcp-pytest-bridge.md` and `AGENTS.md`: PyTest is in-repo (after P4).
- New `docs/agents/release.md`, the one runbook 0059 promises: the release steps, each
  marked by hand or automatic (draft with `build -- changelog`, bump PR, `gate`, Run
  workflow with the product, environment approval, then publish, `channels`, and
  `verify-install`), plus hotfix from `release/<product>/X.Y`, rerun after a failure, and
  the cross-product order. Linked from `docs/agents/index.md` and `AGENTS.md`. Write it
  early (with P3b) and correct it after each first release.
- Owner: tag rulesets (`daemon/*`, `addin/*`, `nettest/*`, `pytest/*`), protection
  of `develop` with `ci / gate`, retire `main`.

## Release order (first releases under the new scheme)

1. `daemon` `4.0.0-beta.8` (channel index provider).
2. `addin` `4.0.0-beta.8` (framework runtimes removed per plan 0060, `minUpdaterVersion` = that Daemon).
3. `nettest` `0.3.0` with TestRunner **and** the framework runtimes of plan 0060, in one release, so users
   change their setup once. A `0.3.0-beta.N` may go out with the `addin` beta (L22).
4. `pytest` `Q4` (first release from this repo).
5. `daemon` and `addin` `4.0.0` after the beta proof (P6). The `addin` bump PR moves the
   `PublicAPI.Unshipped.txt` lines into `Shipped` (0059 §6).

Users of shipped versions reinstall `Setup.exe` once (L1).

## Risks And Recovery

- Bad `channels.json` write: rerun the `channels` job from the previous release.
- A product release fails mid-publish: `publish` skips versions the registry has; the
  GitHub Release stays draft until all assets upload (feeds last, as today).
- Path declarations miss an input: CI skips a job it should run. Mitigation: paths
  come from the `ProjectReference` closure (L25), root build files belong to every .NET
  product, and every .NET job checks `product-paths.yml`. An input outside both (a file a
  target reads by path) is added to the product's extra paths when found.
- `channels.json` and its signature are swapped as two uploads: a client in between
  fails the signature and retries next cycle. Recovery: none needed; a stuck pair is fixed
  by rerunning `channels`.
- A `ProtocolRange` `Min` change slips under a minor: `ci` and `release` warn, they do not
  block; review is the gate (0055 §3).
- Rollback of the pipeline itself: there is no old pipeline to fall back to (L27). Between
  P1 and P6 no `daemon`/`addin` release can be cut; a fix needed in that window waits for
  P6 or is a revert of the phase PR. Accepted: no user is on a supported channel that
  expects one.

## Progress

- [x] P0 spikes (2026-11-08; Q2: delta filter in P5)
- [x] P1 build products and versions (merge `25debcfe`; `d90962d9`..`c73dd379`)
- [x] P2 ci.yml and gate (same merge; GitHub-side proof waits for the first push)
- [x] P3a TestRunner in nupkg (`d075513b`; live `dotnet test` on Revit 2024, 2025, 2027 on 2026-11-08)
- [x] P3b release.yml + nettest (merge on 2026-11-08; `publish nettest` is a build target, YAML holds only OIDC login and artifacts; Actions dry-run waits for push and the `nettest` environment)
- [x] P4 PyTest move + pytest product (merged 2026-11-08; history rewritten under the folder with `filter-branch`, 45 commits; `ci.yml` calls `build -- ci pytest`; `release.yml` job `publish-pytest` on ubuntu)
- [x] P5 channels.json + update engine (unit proof; the local Daemon run against a served index waits for P6 assets)
- [x] P6 daemon and addin products, code (2026-11-08; `publish`, `channels`, `verify-install` in `release.yml`; local delta pack proven; first real releases wait for the owner)
- [x] P7 changelog targets (merge `9476c818`; include paths are the product's project folders; first release sections are written in each product's release PR)
- [x] P8 cleanup and docs (2026-11-08; old names gone outside decisions and plans; owner settings listed in `docs/agents/release.md`)
- [ ] Owner: environments, secrets, trusted publishers, tag rulesets, `ci / gate` required; then first releases in the order below

## Decisions

- 2026-11-06: Environments are named after products (`nettest`, `pytest`), so the
  PyPI publisher uses environment `pytest` (ADR 0059 §4 corrected).
- 2026-11-08 (owner): 0059 accepted. Trunk `develop` (L19), first versions (L20),
  bump rules (L21), `nettest` publish after `addin` (L22), testing product paths (L23).
  PyTest moves into this repo (L14, confirmed). The release-pipeline and release-proof
  parts of the auto-update plan move here; that plan keeps the engine features.
- 2026-11-08: Phases P0–P8 written to implementation detail from a read of `build/`,
  `.github/`, `DevTools.Update`, and the PyTest repo (facts at the top of Approach). New
  plan decisions L24–L26; Q2 narrowed; Q4, Q5 opened. 0059 §5 (local version form),
  §6 (the bump PR moves `PublicAPI`), §7 (which job holds no secret) corrected to match.
- 2026-11-08 (owner): clean cut, the new flow is the baseline (L27). Aliases, side-by-side
  old workflows, registry and unprefixed-tag reads, `LegacyInnoInstall`, and the root
  `Changelog.md` go; each phase deletes what it replaces, P1 and P2 land as one PR, P8 is
  docs and owner settings only. 0059 §5 refusal and "What is removed" updated.
- 2026-11-08 (owner): Q4 `pytest` first version `0.6.1`; Q5 pixi only.
- 2026-11-08 P0 item 4 (git-cliff 2.14.2 through `pixi exec`): passed. `--tag-pattern
  '^nettest/v'` starts the range at the newest `nettest/v*` tag and ignores a `daemon/v*`
  tag on a later commit; repeated `--include-path 'source/<project>/**'` keeps only product
  commits; `--unreleased` writes `## [Unreleased]`. Config that worked: conventional
  commits, `filter_unconventional`, parsers `^Merge` skip, `^feat` Added, `^fix` Fixed,
  `^(perf|refactor)` Changed, `^(chore|ci|test|docs|build|style)` skip; group order by an
  HTML-comment prefix; breaking lines prefixed "Breaking: " in the template. This is the
  P7 `cliff.toml`.
- 2026-11-08 P0 item 1 (partial): `gh release create spike/v0.0.0-spike.1 --prerelease
  --latest=false` works with a slash tag; `releases/latest` stayed `3.0.0`; the API
  `browser_download_url` is `…/releases/download/spike/v0.0.0-spike.1/a.txt` (raw slash);
  `gh release download spike/v0.0.0-spike.1` returns the file. The direct download URL
  could not be checked: every `github.com/…/releases/download/…` URL, including
  `cli/cli`, answered 504 from this machine. Closed: the owner opened the direct URL in a
  browser and the file downloaded. Item 1 passed; spike release and tag deleted.
- 2026-11-08 P0 item 3 (vpk 1.2.161): passed. Pre-filling the pack folder with the previous
  release's `releases.<channel>.json` and `*-full.nupkg` makes `vpk pack` write a delta.
  The new feed lists the new full, the new delta, and the previous full (file not in the
  release); vpk also writes `assets.<channel>.json`, added to the asset list.
- 2026-11-08 P0 item 2 (Q2): the plan's expectation was wrong; Velopack does **not** fall back.
  A 0.0.1 client reading the 0.0.3 folder took the one 0.0.3 delta (built on 0.0.2) as the
  whole chain, applied it to 0.0.1, reported 0.0.3, and kept a stale 0.0.1 file. No 404,
  no error. A source that drops deltas unless the feed lists the full of the installed
  version gave the right result for 0.0.1 → 0.0.3 (full) and 0.0.2 → 0.0.3 (delta). The
  filter goes into `VelopackReleaseSource` in P5. P0 done.
- 2026-11-08 P1+P2 landed with these departures from the text above:
  - Tests run in Debug through `dotnet run --project <p> -f <tfm> --no-build`; Release
    compiles out asserted diagnostics, and two MSTest runners are console apps.
  - Test projects that reach a host project build in `Debug.Autodesk.2027` with `-m:1`;
    year configurations share one `obj` per library and raced (CS2012).
  - A test project belongs to a product when it references a project in the product's closure;
    product paths add the test folder and its `tests/` helpers, not the test's references.
  - Every .NET product also lists `.editorconfig`, `NuGet.config`, `tests/*`, and
    `.github/product-paths.yml`.
  - `AddinVersion.props` reads a `ProductVersion` property so `-p:Version` does not stamp
    every project of the solution build.
  - `samples/Directory.Packages.props` floats on `0.0.0-local.*` with `RestoreForce=true`;
    restore otherwise skips a newer local pack.
  - Daemon packs use `0.0.1-ci.*` / `0.0.1-local.*` (vpk floor); no `+sha` in computed
    versions (the SDK adds it).
  - `ci`/`pack`/`release pytest` stop with one sentence until P4; `ci.yml` runs the pytest
    job only when `source/RevitDevTool.PyTest/pixi.toml` exists.
  - The Daemon csproj drops `SetTargetFramework` on single-target FileMetadata references
    (double build raced). Docs build on any `docs/**` change and deploy on every push to
    `develop`.

- 2026-11-08 P5 landed with these departures from the text above:
  - A missing signed file (manifest, index, feed) is an `UpdateIntegrityException` from
    `ReleaseReader`; before the first `channels` release the cycle fails and backs off.
  - `UpdateThrottledException` and the `retryAfter` backoff went with `GitHubReleaseProvider`;
    no request is a GitHub API request now.
  - `DaemonUpdater` sets the Velopack channel explicitly (`prerelease` or `win`), so a
    prerelease install reads `releases.win.json` when its channel points at a stable Daemon.
  - The Daemon updates only when the channel's `daemon` version is newer than the installed one.
  - The writer lives in `build/Products/Channels.cs`; its URL check asks for
    `RevitDevTool-Setup.exe` in the `daemon` folder and the `addin` manifest. `build/Products/Command.cs`
    runs `git` and `gh`. Downloads base comes from `UpdateEngineOptions.ChannelIndexUrl`.
  - The Updates tab shows "DevTools Daemon <own version>, latest <channel daemon>" above the
    options; per-host add-in versions were already there.

## Validation

- Focused proof per phase: `build/` unit tests for version grammar, previous-tag
  lookup, product declarations; `dotnet run --project build -- release <product> --dry-run`
  output for each product.
- Integration: one real `release.yml` run per product to a prerelease version; the
  `verify-install` job for `daemon`/`addin`.
- Repository-required checks: compile per `.agents/skills/build/SKILL.md` after
  every `.cs`/`.csproj` edit.

## Result

Not started.
