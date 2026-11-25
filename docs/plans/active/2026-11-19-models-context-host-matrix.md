# Execution Plan: Models Context Test Matrix on Revit 2026

Date: 2026-11-19

## Status

Active

## Outcome

On Revit 2026 with the Snowdon sample, one documented flow proves the panel is:

- **safe:** no Revit crash, and the model is left unchanged;
- **correct:** the panel agrees with an API oracle, and every difference from Revit's own view filters is explained;
- **fast enough:** timings are judged against what a user notices;
- **stable:** it holds up under real user actions (edits, link reload, view and document switches).

The run ends in one published report. The report states:

- what was tested and how long each step took;
- whether that time is noticeable or annoying for a user;
- how far the filter results can be trusted;
- how the panel compares with the filter UI built into Revit.

The filter engine is also proven headless, from a JSON filter, which is the path a future MCP tool takes.

## Context

- Policy: [0062](../../decisions/0062-model-context-panel.md). Behavior:
  [product/models-context.md](../../product/models-context.md). Structure and
  gotchas: [architecture/ModelsContext](../../architecture/ModelsContext/README.md).
  Current suites: [test-matrix.md](../../architecture/ModelsContext/test-matrix.md)
  (L2 130/130 and L3 6/6 on Revit 2026, run `20261118-103516`).
- Fixed goals: every panel operation is safe for Revit, and the core stays usable
  by a future MCP tool that reads and creates filters (the panel as live context
  for agents, no code or re-query per question).
- Sample: `C:\Program Files\Autodesk\Revit 2026\Samples\Snowdon Towers Sample HVAC.rvt`
  with its Architectural link. The Program Files files are never saved.
- Decided 2026-11-19: no generated models; Revit 2026 + Snowdon first, other
  years later with the same cases and per-year expected values.

## Matrix

Axes. A test row names one value per axis.

| Axis | Values |
|------|--------|
| Documents | Current model (HVAC), link (Architectural), both, reload link (setup below) |
| Scope | Whole model, Active view (`L3` plan, `{3D}`), Selection |
| Filter source | Custom rules, Revit rule-based filter, Revit selection filter, JSON (headless) |
| Model state | Fresh, after an edit, after a link reload (same, small, large change), after a view switch, after a model switch, pane hidden then shown |
| Cache | Cold (first use), warm (repeat), invalidated (after a change) |

Test groups:

| Group | Ids | What it proves | Layer | Status |
|-------|-----|----------------|-------|--------|
| Correctness | SC, EX, TX, NU, IN, YN, EN, RF, ST, GR, GS, PK, HF, LU, PS | Counts per scope, document, operator and kind = oracle = recorded value | L2 | Exists (130) |
| Native comparison | NF-01… | For each filter case that a Revit view filter can express: panel, oracle and Revit native (`ParameterFilterElement` in a rolled-back transaction) agree, or the difference is listed with its reason (case, type parameters, empty text, design options, links) | L2 | New |
| Random filters | RT-01… | 200 seeded random trees from the Snowdon catalog: depth up to 5, up to 50 rules, every operator, Aa, group scopes; panel = oracle; a failing tree prints as a ready case | L2 | New |
| Headless (MCP path) | MC-01… | A JSON filter run through the core ports with no panel view gives the panel's result; a call while the panel is busy stays consistent and safe | L2 | New |
| User actions | US-01… | Edits, undo/redo, view switches (plan, 3D, sheet, schedule, drafting, legend), pane hide/show, fast repeated changes, link reload (same, small, large), model switch: count = oracle, panel Idle, no stale rows, time per step | L2 + MCP steps | New |
| Visibility | VI, UI-05, UI-06 | Tiers T0–T3, Keep and Reset | L2 + L3 | Exists |
| Panel flows | UI-01…UI-06 | UIA input to count | L3 | Exists |
| Performance | PF-01… | Cold, warm and invalidated time per operation (snapshot, quick counts, catalog, compile, match, value list); Revit-thread queue and run; longest UI stall; memory per million; baseline for 2026 | L2 + L3 + sandbox | New |
| Safety | SF-01… | Each run: Revit alive and responsive, no fatal journal entry or new crash dump, the samples not modified, no temporary mode or Reveal Hidden left, selection restored, panel Idle with no error | All live runs | New |

### Reload link setup

Copies only, in `artifacts/models-context-work/2026/` (ignored by git):

- The HVAC and Architectural files (so the relative link resolves).
- `Reload-Link.rvt`, a copy of `Snowdon Towers Sample Structural.rvt`, linked into the HVAC copy as an extra independent link.
- `Reload-Link-small.rvt`: a few elements changed, added and deleted.
- `Reload-Link-large.rvt`: over 20% of the elements changed, so the panel rebuilds.

A setup script run once through MCP `execute_csharp_code` builds them. A run reloads the extra link from the small and the large copy (`LoadFrom`) and back as MCP steps between test runs. Gotcha 20 forbids opening a document inside a run. The model switch uses a second open copy the same way.

### How timings are judged

The report rates every user-facing time against fixed thresholds:

| Time | User experience |
|------|-----------------|
| Under 100 ms | Instant |
| 100–300 ms | Noticeable, fine |
| 300 ms–1 s | Slow; needs the progress bar |
| Over 1 s | Annoying unless it shows progress and leaves Revit usable |
| Any stall of the UI or Revit thread over 50 ms | A visible hitch |

The 0062 budgets and the committed 2026 baseline give pass or fail.

## Approach

One step at a time. Each step stops for review. Nothing is committed unless asked.

1. **Setup** (main session, live): copy the files, run the reload-link setup script, record the element counts of each copy.
2. **Native comparison, random filters, headless** (one agent, then a live run): new L2 classes beside `FilterMatrixTests`. They reuse `Oracle` and the case JSON.
3. **User actions and safety guards** (one agent, then a live run). The guards go into `Evidence`, so every L2 and L3 run gets them.
4. **Performance** (one agent, then a live run): timings from the panel's Trace lines (`TraceLines.cs`), a Trace-only UI stall heartbeat in the panel, and `perf/2026.json` written by a record switch for review.
5. **Full run and report** (main session):
   - run `scripts/test-models-context.ps1` on a fresh Revit 2026;
   - after it, the reload and model-switch steps;
   - publish the report artifact;
   - update test-matrix.md with the new groups and manual steps.

Later, tracked in Progress: the same cases on Snowdon 2024, 2025 and 2027 with expected values per year; Revit 2023 on another sample. Live tests cover Revit 2026 only until then. The idea when it starts: case JSON says what to check, a per-sample facts file holds the model data, expected values are keyed by year, and a record mode writes them from one live run. Not in this plan: the MCP filter tool itself.

## Risks And Recovery

- A new test path crashes Revit: every new class first runs alone on a fresh Revit. A crash becomes a gotcha and a safety case, never a retry loop.
- Reload or model switch inside a run is unsafe (Gotcha 20): they stay MCP steps between runs.
- The native comparison writes a `ParameterFilterElement`: always in a transaction that is rolled back; the safety guard checks the model is unchanged.
- Recovery: changes stay uncommitted until the user asks; each step is a separate diff to review or revert.

## Progress

- [x] 1 Setup copies and reload link.
- [x] 2 Native comparison, random filters, headless.
- [x] 3 User actions and safety guards.
- [x] 4 Performance (baseline file `perf/2026.json` not recorded yet).
- [x] 5 Full run `20261119-012603` (L2 434, 10 fail: 9 RT, 1 memory budget; L3 13/13; safety ok), report artifact https://claude.ai/artifact/LY7pDBtvo2451mJgZdmna8, Gotchas 21–22.
- [x] Round 2 (run `20261119-121344`): E1 sampled built-ins verified, E2 materials excluded (SC-01 2,929, SC-02 7,526), E3/E4 `ParameterLocator` probe and `Lacking`, E5 view guard, H1 split prepare, H2 invalidation by change kind (advisor-reviewed). L1 358/358, Views 103/103, L2 434/435, RT 200/200, L3 12/13 (UI-02 flaky), guard test passes.
- [x] Round 3 (run `20261119-140304`): resolve split into `Locate`, `WithTypes`, `WithLacking` calls (longest call NU-04 125→80 ms, invalidated 160→76, ST-02 84→55, GR-03 85→53); catalog verify filters built once per id (Arch cold 198→139 ms); memory budget judged at 100,000 elements at least (Arch 7,544 elements gave 66 MB per million from fixed costs); UI-02 connector click retried until OR; Trace `ChangesUnavailable`; PreparesSample reruns until the sample view is active (SC-05 saw the Cover Sheet the copy opened on). L1 358/358, L2 430/430, L3 13/13 on rerun (UI-03 once read "t" in the parameter box, a stray key on the shared desktop), safety ok. The guard's refusal of inline callers after a user view switch stays by choice (Gotcha 21).
- [x] Confirmation run `20261119-162831` on a fresh Revit 2026, no reruns: L1 358/358, L2 430/430, L3 13/13 first time, safety ok at all three checks. Longest calls NU-04 72/73 ms, ST-02 54/82 ms (invalidated, one-off), GR-03 53/57, SC-02 invalidated 52; ratings unchanged (44 instant, 8 noticeable); repeat memory +0.02 MB.
- [x] Round 4 live (runs `20261119-170358` federated, `-171559` Golden Nugget, `-172213` Japanese): US-13, US-14, US-17 pass; US-16 finds that an unsaved draft filter is dropped without asking on a model switch (`FilterState` keeps only the selected item). SW-01 counts match everywhere (schedules 38/39, one phase-filter difference). SW-03 295/300. SW-04 longest call 95 ms, nothing slow. SW-02 three-way: panel bug on computed numbers "has value" (holds branch: Electrical 200 vs 36); oracle bugs on Type IfcGUID and computed "empty"; decision needed on numbers compared by displayed value (3 mm shown, 2.54 mm stored) and on None references as empty. Test fix: `NativeFilter.SpecOf` took the parameter id, not the spec.
- [x] Round 4 fixes (offline, live check pending): Properties is the truth. Oracle compares typed numbers by displayed value and reads a built-in the type lists from the type (Type IfcGUID); native uses half a display step and samples the spec of a built-in; sweep reason `none reference`; panel `Held` (Value, Empty, Absent from 25 blind samples) and `Silent` elements (per-element listing, lazy, sliced); oracle reads a built-in per element (listed or seen by a native rule) ([filters.md](../../architecture/ModelsContext/filters.md#compiling-a-rule-filtercompiler)); model switch keeps the unsaved draft per model. L1 361/361, Views 103/103.
- [x] Round 5 live (runs `20261119-223247`, `-224510`, `-225355`, `-231009`, `-232819`): HVAC L2 430/430, L3 13/13, safety ok; US-16 draft survives a model switch (Ducts + Comments, count 3). SW-02 panel = oracle on all four models (0/3,091 federated, 0/3,510 Golden, 0/2,932 Japanese), SW-03 300/300, SW-01 counts all equal. Last fixes: a shared parameter Properties does not show is undefined in the oracle (curtain panels that are walls); a key with several ids is empty only when every id is empty or missing (Japanese rooms, two "仕上表 グループ名"). NU-04 longest call about 95 ms (round 3: 72–80), from the `Held` sampling.
- [ ] Open: per-version golden values (the 2024 sample gives SC-02 7,700, SC-06 765, UI-04 7,700), then the Revit 2023 (minimum), 2025 and 2027 live runs, then 0062 Accepted.
- [ ] Open: link Hide 534 ms and Show 2,420 ms on the Revit thread (UI-05/06, mostly Revit regeneration). Low priority: NU-04 about 95 ms in one call (round 3: 72–80 ms).
- [x] By design: 17–32 rows per model where panel = oracle but Revit's native rule differs (Workset, parameters no native rule sees). The panel follows the Properties palette; revisit only if panel filters are exported as Revit view filters.
- [x] Numbers compare at display precision: on Golden Nugget a Revit schedule "Lower End Bottom Elevation ≥ 609 mm" returns 1,127 pipes, the same as the panel's half-step rule (exact compare gives 1,126; pipe 3593367 stores 608.96 mm and shows 609).

## Round 4: broader and heavier

The suites so far use two documents (HVAC and its Architectural link) and a few
categories (doors, ducts). Round 4 widens the models, sweeps every category and
parameter, and closes the four untested user actions.

Models (shipped samples, copies in `artifacts/models-context-work/2026/`, never saved over the originals):

| Id | Active model | Links | Why |
|----|--------------|-------|-----|
| M1 | Snowdon HVAC federated copy | Architectural, Structural, Electrical, Plumbing, Facades, Site | 7 documents, every discipline |
| M2 | Golden Nugget Gebaeudetechnik | Golden Nugget Architektur (131 MB) | German parameter names, metric units, the largest sample |
| M3 | サンプル設備 | サンプル意匠 | Japanese names, non-ASCII titles |

Sweep (L2, explicit, no recorded values: panel = oracle on whatever model is active, with all its loaded links):

- SW-01 counts: every category of every document, Whole model and Active view.
- SW-02 parameters: every category with elements (no size cap), every catalog parameter, operators Has value, Has no value, Equals the most common value, Contains (text), at least the median (number); the matched id sets must be equal. Mismatches go to `sweep-mismatches.json`, the test lists the first ones.
- SW-03 random trees over all documents and all categories, large categories included.
- SW-04 timings per document (snapshot, counts, catalog, values, filter) cold and warm, longest call, memory per million, in `sweep-perf.json`.

User actions (MCP steps between runs, the live panel read through UIA):

- US-13 real Undo and Redo: an MCP edit commits Comments on three ducts, then `navigate_history` back and forward; the panel count follows 0, 3, 0, 3, 0.
- US-14 pane hidden during a change: hide the pane, commit the edit, show it: the count is right after one resync.
- US-16 model switch: open a second model, the panel shows that model; switch back, the HVAC session returns.
- US-17 view template that controls V/G: Hide, Isolate, Halftone, Color, Keep and Reset on host and link elements in a view whose template locks V/G: no exception reaches Revit, a refused action gives one clear message, Reset leaves the view as before.

## Decisions

- 2026-11-19: Revit 2026 and Snowdon first; no generated models; other years later with the same cases.
- 2026-11-19: reload tests use copies with an extra independent link (a copy of Snowdon Structural).
- 2026-11-19: Revit's own view filter is a second reference, beside the oracle, for the robustness comparison.
- 2026-11-19: no commits unless the user asks.
- 2026-11-19: the panel follows the Properties palette where Revit's view filter disagrees: numbers by displayed value, a *None* reference is empty, a model switch keeps the unsaved draft without asking ([0062](../../decisions/0062-model-context-panel.md)).

## Validation

- L1 and Views tests after any shared change.
- Live: `scripts/test-models-context.ps1 -RevitPid <pid> -Year 2026`, the new classes, then the MCP reload and switch steps.
- Report: the published artifact plus `artifacts/models-context-tests/<run>/report.md`.
- `pwsh -File scripts/format-lint.ps1` once at the end.

## Result

Complete after implementation.
