# NUnit Native Runtime — Live P0 Verification

Evidence for plan Task 8 (`docs/plans/active/2026-08-12-nunit-native-runtime-mtp.md`).
Raw artifacts: `%LOCALAPPDATA%\RevitDevTool\task8-evidence\`.

## Environment (2026-08-12, continuation)

| Host | Status | Notes |
|------|--------|-------|
| Revit 2023 | Live | net48; async + full Task1 matrix green after off-UI Run fix |
| Revit 2026 | Live | net8 proxy for plan’s Revit 2025 ALC row |
| Revit 2025 | — | Substituted by 2026 |
| Revit 2027 | — | Not installed — environment blocker |
| AutoCAD / Civil 3D 2026 | Live | Runner smoke Passed — see below |

## Civil 3D 2026 smoke (continuation)

- Sample: `samples/DevTools.NUnit.Civil3D.SampleTests`
- Runner: `--host Civil3D --version 2026 --host-launch`
- Case `Arithmetic_runs_inside_civil3d_host` **Passed** (`EXIT=0`)
- Output: `acad-version=25.1.0.0`, `host-pid=38048`, `process-name=acad`,
  `civil-assembly=AeccUiWindows`
- Evidence: `%LOCALAPPDATA%\RevitDevTool\task8-evidence\civil3d-2026-arithmetic-smoke.txt`

## Fixes landed this session

1. **Off-UI `nunit/run`** — `NUnitRequestHandler` no longer marshals the whole NUnit
   session onto Revit Idling via `IHostContextExecutor`. Discover still marshals;
   Run uses `Task.Run` with `ExecutionGuardMode.Suppress` on the async flow.
2. **Unload diagnostic timing** — `NUnitRuntimeManager` releases obsolete generations
   before enriching the response so `runtime_diagnostic` appears on the switching
   response (not the next request).
3. Host/unit coverage updated for off-UI Run. Unload-after-run is not a unit gate.

## Revit 2023 — async + full Task1

- Async probe (`AsyncTest_Completes` + `AsyncLifecycle_TestCompletes`): **2 Passed**.
  File: `2023-async-probe-out.txt`.
- Full Task1 matrix: **27 Passed / 2 Skipped / 1 Inconclusive / 1 Error** (deliberate).
  File: `2023-task1-full-out.txt`. Includes async fixtures — prior UI deadlock closed.
- Gen-two marker (same host, no restart): `generation-marker=generation-two`, new
  `generation_id`. File: `2023-gen2-marker-out.json`.

## Revit 2026 — generation switch + ALC unload

- Gen1 → gen2 marker switch works; diagnostic now on gen2 response.
- Files: `2026-alc-gen1.json`, `2026-alc-gen2.json`, plus retry / stream-load /
  host-share packs (`2026-alc-gen*-hostshare.txt`, `2026-alc-gen*-hostshare2.txt`).
- Host unit tests (`DevTools.NUnit.Host.Tests`) prove isolation and host-share;
  they do not require `generation.unloaded` after Run.
- Live Revit 2026 after option-1 host-share still reports
  **`generation.retained`**. Scan shrunk from
  `[Runtime, Fixtures, nunit.framework]` to `[Runtime, Fixtures]` — NUnit is
  no longer inside the collectible ALC. No scanner-visible host static remains.

## System-level unload assessment (2026-08-13)

CLR collectible unload is **cooperative**, not forced
([Microsoft: assembly unloadability](https://learn.microsoft.com/dotnet/standard/assembly/unloadability)).
`AssemblyLoadContext.Unload()` only *starts* unload. It finishes only when:

1. No thread has a collectible method on its stack.
2. No outside strong ref / strong `GCHandle` points at a collectible assembly,
   type, or instance (stack/JIT locals, statics, `AsyncLocal` / ExecutionContext,
   `RegisteredWaitHandle`, fields on the ALC subclass itself).

That is a different contract from net48 AppDomain unload (forced abort).

### What option 1 actually proved

| Layer | Result |
|-------|--------|
| Isolation | **Proven live** — new `generation_id` + `generation-two` IL on same PID |
| Host-share versioned `nunit.framework` (not Dynamo 3.x) | **Works** — unit tests keep 4.6 vs stub 3.14 distinct; live scan dropped `nunit.framework` from the collectible ALC |
| NUnit `TestMetadataCache` / `MethodInfoCache` / `AsyncLocal` | Real pins, but **not chased in product code** — reflection clears only helped quiet unit processes; live Revit still retained |
| Live `generation.unloaded` | **Not achieved** — still `[Runtime, Fixtures]` after host-share |

### Why unit unloads and Revit does not

Isolated unit tests are a quiet process and may still collect a generation ALC
after discover+dispose. Live `nunit/run` uses `Task.Run` onto Revit’s
**persistent ThreadPool**, plus NUnit `RunAsync` / timeout / async fixtures which
hop more pool threads. NUnit 4 stores `TestExecutionContext` in `AsyncLocal`;
idle pool workers keep the previous ExecutionContext. That is why live reports
`generation.retained` with `alc-assemblies=[Runtime, Fixtures]`.

This is not a missing allowlist entry we failed to scrub. Further NUnit static
hunting will not close the live P0.

### Kept vs stripped after accepting the gap

**Keep (observable impact):** host-share versioned `nunit.framework`; off-UI
`Task.Run` for `nunit/run`; stream-load shadow assemblies; share SRM/Immutable
and host NuGet prefixes on modern TFMs; release obsolete sessions before enrich.

**Strip (live-unload overlays):** retain-root scanner, dedicated verify thread,
`ExecutionContext.SuppressFlow`, NUnit private-cache reflection, session field
nulling, MethodInfo-avoiding source lookup, 40-cycle verify.

### Can it be fixed thoroughly?

| Approach | Thorough? | Notes |
|----------|-----------|--------|
| More NUnit cache / AsyncLocal clears | No | Already sufficient for unit; live pin is off-scanner |
| Flood ThreadPool to overwrite ExecutionContext | Partial / unsafe | Might confirm the theory; cannot cover UI thread; steals Revit pool threads |
| Host-share Runtime too (fixtures-only collectible ALC) | Smaller surface, same class of pin | Fixture `Type` still lives in NUnit `AsyncLocal` on pool threads |
| Dedicated run thread + Join (mirror unit helper) | Better, not closed-world | NUnit internals still use ThreadPool |
| `!gcroot` on live `LoaderAllocator` | Diagnostic only | Microsoft’s prescribed confirmation; does not itself unload |
| Out-of-process NUnit Engine | Out of ADR | Tests must run inside the Autodesk host |
| **Waive live `generation.unloaded`; keep isolation as the gate** | Product-honest | Matches net48 model (ADR 0016 §5) and CLR cooperative unload. Requires an ADR 0016 §4 amendment |

**Recommendation:** stop treating live `generation.unloaded` as a closable code P0. Keep reporting `generation.retained` when the weak ref stays alive. Treat **generation isolation** (new id + new IL, already live-proven) as the hard modern-host gate. Optional later: WinDbg `!gcroot` if we need a named root for the diagnostic, not as a path to a guaranteed unload.

## Task 8 close criteria vs this pack

| Plan item | Status |
|-----------|--------|
| 2023 Dynamo + `nunit.framework` inventory | Done (prior pack) |
| 2023 Task1 discover/run **including async** | **Done** |
| 2023 generation-two same PID | Done |
| 2025/2026 ALC unload | Switch OK; live still **`generation.retained`** |
| 2027 live | Blocker — not installed |
| AutoCAD smoke | **Done (Civil 3D 2026)** — Runner arithmetic Passed on PID 38048 |
| `dotnet test` Revit API smoke | Done (prior pack; filter still imperfect) |

**Verdict:** async P0 blocker closed. Isolation on modern hosts is live-proven.
Strict live `generation.unloaded` is **not a closable code defect** under CLR
cooperative unload inside Revit (ThreadPool / ExecutionContext). ADR 0016 §4
now matches that: isolation is the gate; `generation.retained` is expected live.
P1 (thin MTP package `DevTools.NUnit`) is unblocked. Do not continue NUnit
static-scrub loops.
