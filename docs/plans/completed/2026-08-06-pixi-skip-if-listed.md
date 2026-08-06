# Execution Plan: Skip-if-listed + Pixi search-first

Date: 2026-08-06  
Completed: 2026-08-06

## Status

Completed

## Outcome

Land [0014](../../decisions/0014-pep723-skip-if-listed-search-first.md): replace
try-first with skip-if-listed (both backends) and Pixi search-first. Pin Pixi
**0.76.1**. Small ensure/install change — not a host-startup benchmark.

## Done (matches code)

- [x] `PythonInstaller.PixiVersion` = `0.76.1`
- [x] `PyEnvironmentProvider`: `ResolvePythonHomeAsync` / `EnsurePythonHomeAsync`,
      read-only `IsEnvironmentReady`, abstract `GetListJsonAsync`, shared
      `GetInstalledNamesAsync`
- [x] Pixi: list → skip; missing → search → add batches
- [x] Pip: list → skip before `pip install` (require ensure + `InstallPackagesAsync`)
- [x] `PythonDepsManager`: `provider.GetListJsonAsync` → Parser stdin (no Backend switch)
- [x] No shell-hook / fake activation
- [x] Tests: `PixiEnvironmentProviderTests`; opt-in `PixiEnvironmentSmokeTests`
      (`RUN_PIXI_SMOKE=1`)

## Result

Ensure/install paths skip when already listed. Warm open still short-circuits
when `PythonHome` is set and `python.exe` exists after setup in-process.
