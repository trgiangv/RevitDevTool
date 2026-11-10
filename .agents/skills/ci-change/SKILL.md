---
name: ci-change
description: >
  Change or debug RevitDevTool CI and release workflows safely. Use whenever a task
  touches .github/ (ci.yml, release.yml, product-paths.yml, ci-jobs.json), the CI side
  of build/ (Products/CiJobs.cs, ProductPaths.cs, ProjectGraph.cs, Product.cs,
  Cli/Targets.cs), adds, moves, or renames a test or source project, investigates a red
  `ci / result` or a slow or flaky CI job, or plans a release run, even when the
  request only says "the pipeline", "GitHub Actions", or "why did CI fail".
---

# CI Change

Facts (flow, jobs, caches, timing) live in `docs/agents/ci.md`; releases in
`docs/agents/release.md`; policy in ADR 0059. Read `ci.md` first. This skill holds
the working rules, and each one comes from a CI run that went wrong.

## Rules

- **Logic goes in C#, not YAML.** A choice that depends on products, paths, host
  versions, or the event belongs in `build/` with a test in `tests/Build.Tests`.
  YAML only wires jobs, steps, and keys. Logic in YAML can't be run or tested locally,
  so each mistake costs a CI round.
- **Generated files come from `build -- paths`.** Never edit `.github/product-paths.yml`
  or `.github/ci-jobs.json` by hand. After you change a project, a `ProjectReference`,
  `Product.cs`, `CiJobs.cs`, or a `Release.Autodesk.*` build type, run
  `dotnet run --project build -- paths` and commit both files. The test stage refuses
  stale copies.
- **Reproduce locally before you push.** For a red job, read its log
  (`gh run view <id> --log-failed`), find the root cause, and reproduce it locally with the
  same `build` command the job runs (`ci <product>`, `test addin`,
  `compile addin --host-version <v>`). A race may need a loop: run the test until it
  fails. Push once, with that evidence. Pushing a guess spends a 5–10 minute run and
  hides the cause.
- **Tests stay hermetic.** A test that writes HKCU, `%AppData%`, a fixed pipe name, or a
  shared file can break on a runner, and only by chance. Inject the key or path instead
  (see `docs/agents/test-matrix.md`). Don't make tests run in parallel across projects to
  save time: many still share pipes and AppData.
- **Respect the cache budget.** Each matrix job owns `nuget-<cache>-<hash>`, and only a
  push saves it. The repository has 10 GB, so don't add a shared or extra cache. A new
  job gets its cache name from `CiJobs.cs`.
- **Keep `result` the one required check.** A new job goes into `result.needs`, so a
  failure or cancellation still turns `result` red.
- **Pin actions by SHA** with the version in a comment, as the existing steps do.
  Give each job `permissions:` and the least it needs. zizmor checks both.
- **Don't add exit-code checks.** The runner's pwsh already exits with the last command's
  code, so `if ($LASTEXITCODE ...)` is noise.

## Before pushing

1. `dotnet run --project build -- paths` if any input of the generated files changed.
2. `dotnet run --project tests/Build.Tests/Build.Tests.csproj` after a `build/` change.
3. `docker run --rm -v "${PWD}:/repo" -w /repo rhysd/actionlint:1.7.12 -color` after a
   `.github/` change. If Docker is not running, ask the user to start it; don't push
   unlinted YAML.
4. For a product failure, the local `build` command that failed on CI now passes.
5. `pwsh -File scripts/format-lint.ps1` once, at the end.
6. Fold the fix into the commit it corrects, then push with `--force-with-lease`.
   Commit messages: a subject plus 2–5 bullets.

## After pushing

- Watch with `gh run watch <id> --exit-status`, then list per-job times:
  `gh run view <id> --json jobs --jq '.jobs[] | "\(.name)\t\(.conclusion)\t\(.startedAt)\t\(.completedAt)"'`.
- A change meant to speed CI up needs a before and after time for the job it targets.
  Update the timing table in `ci.md` when it moves.
- A "Base … is not in this clone" bump warning right after a force push is expected.

## Release workflow

`release.yml` runs `release <product>` on one runner, then publishes after an
environment approval (`docs/agents/release.md`). Treat a change there like a CI change,
and also:

- Secrets reach only `publish` and `channels`. `build` stays secret-free.
- Check locally with `dotnet run --project build -- release <product> --dry-run`. Ask
  before you start a real release: it publishes outside the repository.

## Update docs

When observable CI behavior changes (jobs, triggers, caches, timing), update
`docs/agents/ci.md`. Update `release.md` for release steps, and the ADR only for policy.
