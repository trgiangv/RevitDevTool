# 0014 Skip-If-Listed + Search-First (Pixi/Pip)

Date: 2026-08-06  
Updated: 2026-08-06

## Status

Accepted

## Context

Install/ensure used **try-first** (`pixi add` / `pip install` even when packages
were already present; Pixi also failed-then-retried channel selection). Prefer
**skip when listed**, then classify missing packages once (Pixi).

This is a small ensure/install improvement. Warm host open still depends on
`IsEnvironmentReady()` (home set + `python.exe`); it is not a startup rewrite.

## Decision

1. **Skip if listed (Pixi and Pip)** — `GetListJsonAsync` (`pixi list --json` /
   `pip list --format=json`) supplies installed names before require-ensure,
   `InstallPackagesAsync`, and PEP 723 resolve (`PythonDepsManager` → Parser).
2. **Search-first (Pixi only)** — for missing specs: `pixi search --limit 1`
   (exit code) → at most one conda batch and one PyPI batch. Not fail-then-retry
   as primary. Pip is PyPI-only: list-skip then `pip install`.
3. **Provider shape** — `PyEnvironmentProvider` owns process-scoped
   `PythonHome` via abstract `ResolvePythonHomeAsync` + `EnsurePythonHomeAsync`
   (assign once). `IsEnvironmentReady()` is read-only. `GetListJsonAsync` is
   abstract on the base (no `Backend` switch in callers).

## Non-goals

- Claiming CLI version bumps as a latency win.
- Pixi shell-hook / fake `CONDA_PREFIX` activation in the host process.

## Consequences

Ensure/install avoid redundant adds and systematic failed solves.
`PixiPackageHelper` may still read list `kind` for remove/update.
