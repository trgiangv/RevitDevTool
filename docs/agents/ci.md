# CI

`ci.yml` checks every PR, merge group, and push to `develop`. `ci / result` is the one required check.
Policy: ADR [0059](../decisions/0059-independent-product-releases-and-one-gate-ci.md). Releases: [release.md](release.md).
How to change CI: the `ci-change` skill.

## Flow

```
changes ──► product (matrix, one runner per job) ──► Autodesk bundle (push, addin)
        ├─► docs ──► docs-deploy (push)
        └─► workflows (actionlint, zizmor)
all ──► result
```

| Job | Runs when | Does |
|-----|-----------|------|
| `changes` | always | `dorny/paths-filter` with `.github/product-paths.yml`, then picks the changed products' jobs from `.github/ci-jobs.json` with one `jq` lookup. `workflow_dispatch` takes every product |
| `product` | a product changed | Restores its NuGet cache, runs `dotnet run --project build -- <run>`; on push saves a new cache key, packs, and uploads |
| `Autodesk bundle` | push, `addin` changed | Downloads every `addin-<v>` into `output/hosts`, then `pack addin` |
| `docs`, `docs-deploy` | `docs/**` changed | Vite+ build; deploys Pages on push |
| `workflows` | `.github/**` changed | actionlint 1.7.12 and zizmor (high) |
| `result` | always | Fails when `changes` did not succeed or any job failed or was cancelled |

## Jobs per product

`.github/ci-jobs.json` (from `build/Products/CiJobs.cs`):

| Product | Job | Command | Cache |
|---------|-----|---------|-------|
| `daemon` | `daemon` | `ci daemon`, then `pack daemon` on push | `daemon` |
| `addin` | `addin tests` | `test addin` | `addin-tests` |
| `addin` | `Autodesk <v>`, one per `Release.Autodesk.*` in `RevitDevTool.slnx` | `compile addin --host-version <v>`; on push uploads `output/hosts` as `addin-<v>` | `autodesk-<v>` |
| `nettest` | `nettest` | `ci nettest`, then `pack nettest` on every event (ADR 0059: the `0.0.0-ci` nupkg) | `nettest` |
| `pytest` | `pytest` | `ci pytest` (pixi), then `pack pytest` on push | `pytest` |

## Generated files

- `dotnet run --project build -- paths` writes `.github/product-paths.yml` (from the project graph in
  `build/Products/ProductPaths.cs` and `ProjectGraph.cs`) and `.github/ci-jobs.json`.
- The test stage and `Build.Tests` refuse a stale copy of either, so a missed `paths` run fails CI, not a silent skip.
- Each test project belongs to one product; see [build-matrix.md](build-matrix.md#commands).
- Files that every .NET product lists (`build/**`, `props/**`, `Directory.*.props`, `global.json`, `tests/*`, both
  generated files) start every .NET job.

## Caches

- Key `nuget-<cache>-<hash of csproj, Directory.Build.props, Directory.Packages.props, dotnet-tools.json>`, restore
  key `nuget-<cache>-`. The bundle job uses `nuget-bundle`.
- Only a push saves, and only when the key is new. PRs restore the `develop` caches.
- The repository has 10 GB of cache; GitHub evicts the least recently used. `release.yml` keeps no cache so it
  cannot evict CI's.

## Timing (2026-11-10)

| Run | Wall time |
|-----|-----------|
| Every job, warm caches | about 10 min |
| Every job, cold caches | about 17 min |
| Daemon only | about 4.6 min |

`addin tests` (about 9.6 min) is the longest job: checkout 46 s, restore 37 s, `build` tool 33 s, builds
`Debug.Autodesk.2027` with `-m:1` (2.2 min, Autodesk configurations share one `obj` folder) and `Debug` (1.5 min),
then 16 test projects one at a time (3.8 min). On push the `Autodesk <v>` plus bundle path takes about 8.5 min.

## Diagnostics

- A test past 5 minutes writes a hang dump; a failed job uploads `**/TestResults/*_hang.*` as `diagnostics-<job>`.
- `test` and `ci` read the bump-warning base from `GITHUB_EVENT_PATH` (`build/Tools/GitHubEvent.cs`). After a force push
  the old base is not in the clone and the warning says so; that is expected.
