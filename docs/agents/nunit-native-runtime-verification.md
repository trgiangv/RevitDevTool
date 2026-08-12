# NUnit Native Runtime — Live P0 Verification

Evidence for plan Task 8 (`docs/plans/active/2026-08-12-nunit-native-runtime-mtp.md`).
Raw artifacts: `%LOCALAPPDATA%\RevitDevTool\task8-evidence\`.

## Environment (2026-08-12)

| Host | PID | Build | Notes |
|------|-----|-------|-------|
| Revit 2023 | 45108 | 23.1.90.15 | net48; Dynamo cores loaded |
| Revit 2026 | 43776 | (running) | net8 proxy for plan’s Revit 2025 ALC row (operator decision: same TFM) |
| Revit 2025 | — | — | Not run; substituted by 2026 |
| Revit 2027 | — | — | Not installed — environment blocker |
| AutoCAD | — | — | Not run — optional / blocker |

## Revit 2023 + Dynamo / `nunit.framework`

Dynamo add-in assemblies loaded in-process (13 name matches including `DynamoCore`, `DynamoRevitDS`, …).

**On disk (Dynamo):**

`C:\Program Files\Autodesk\Revit 2023\AddIns\DynamoForRevit\nunit.framework.dll`  
→ `nunit.framework, Version=2.6.3.13283, Culture=neutral, PublicKeyToken=96d09a1eb7f44a77`

**Loaded in AppDomain after Task1 + gen-two runs:** only DevTools generation copies  
`nunit.framework, Version=4.6.0.0, Culture=neutral, PublicKeyToken=2638cd05610744eb` under  
`%TEMP%\…\DevTools\NUnit\Generations\<generation_id>\nunit.framework.dll`  
(count grew with generations; Dynamo’s 2.6.3 identity was **not** loaded into the AppDomain during this session).

Coexistence claim for this run: Dynamo is loaded; DevTools owns its private 4.6 generation `nunit.framework` identities; Dynamo’s older framework stays on disk until a Dynamo test path loads it.

## Task 1 fixture (net48) on Revit 2023

- Discover: **31** cases, `generation_id=cd071c38…982e`.
- Sync matrix (exclude `AsyncLifecycleFixture` + `FullSemanticsFixture.AsyncTest_Completes`):

| Outcome | Count |
|---------|-------|
| Passed | 25 |
| Skipped (Ignore/Explicit) | 2 |
| Inconclusive | 1 |
| Error (deliberate unexpected exception) | 1 |

Summary file: `2023-task1-sync-out.json`. Host PID **45108**.

### Async hang (open gap)

Running `AsyncTest_Completes` / `AsyncLifecycleFixture` alone times out: NUnit `Run` is marshaled onto the Revit UI executor (`ExecuteAsync`), so `WaitForCompletion` blocks Idling while async continuations cannot complete → **UI-thread deadlock**. Full Task1 including async is blocked until Run is off-UI or async fixtures are adjusted.

## Generation-two (same Revit 2023 PID)

Without restarting Revit 45108:

| Assembly | Marker output | `generation_id` |
|----------|---------------|-----------------|
| staged `net48-gen1` | (prior sync run) | `cd071c38…982e` |
| staged `net48-gen2` | `generation-marker=generation-two` | `0e48aa2f…039a` |

File: `2023-gen2-marker-out.json`.

## Revit 2026 (net8 / 2025 proxy)

- Sync Task1 matrix: same outcomes as 2023 (`2026-task1-sync-out.json`).
- Gen-two marker: `generation-marker=generation-two`, new `generation_id`.
- ALC unload diagnostic after generation switch: **`generation.retained`**  
  (`Generation ALC retained after unload verification.`) — observed twice  
  (`2026-gen2-marker-out.json`, `2026-gen1-again-out.json`).  
  Generation switch works; clean ALC collect was **not** observed.

## `dotnet test` Revit API smoke (2023)

```powershell
dotnet test samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj `
  -c Debug.Autodesk.2023 --filter "FullyQualifiedName~Arithmetic_runs_inside_host"
```

- `Arithmetic_runs_inside_host` **Passed**; stdout includes `23.1.90.15` and `host-pid=45108`.
- Adapter still executed sibling sample tests in the same run (`Intentional_failure_for_demo` failed, `Writes_output` passed) — filter/adapter gap; acceptance evidence is the Arithmetic case + PID match.

## Cancel-on-disconnect (related hardening)

Earlier same day: Infinite-sleep CancelProbe entered on PID 45108 → concurrent discover blocked → kill Runner → discover completed ~8.8s → MCP execute responsive. See session notes; not re-run in this evidence pack.

## Task 8 close criteria vs this pack

| Plan item | Status |
|-----------|--------|
| 2023 Dynamo + `nunit.framework` inventory | Done (Dynamo loaded; 2.6.3 on disk; 4.6 generations in AD) |
| 2023 Task1 discover/run | Sync matrix done; **async excluded** (deadlock) |
| 2023 generation-two same PID | Done |
| 2025 ALC unload | **Proxy 2026**: switch OK, diagnostic **`generation.retained`** |
| 2027 live | Blocker — not installed |
| AutoCAD smoke | Blocker / not run |
| `dotnet test` Revit API smoke | Arithmetic Passed on PID 45108 (filter imperfect) |

**Verdict:** do **not** treat Task 8 as fully closed. P0 live path is largely proven on 2023 + 2026, but async UI deadlock, ALC `generation.retained`, and 2027/AutoCAD blockers remain.
