# NUnit Native Runtime: Live Verification Record

Closed evidence for Task 8 of
[`2026-08-12-nunit-native-runtime-mtp.md`](../plans/completed/2026-08-12-nunit-native-runtime-mtp.md),
recorded 2026-08-12 and 2026-08-13. Raw artifacts:
`%LOCALAPPDATA%\RevitDevTool\task8-evidence\`.

Outcome: the async blocker is closed and generation isolation is proven live on
modern hosts. Live `generation.unloaded` is not reachable under cooperative CLR
unload inside Revit, so [ADR 0016](../decisions/0016-nunit-native-runtime-and-mtp-first-integration.md)
§4 treats isolation as the requirement and `generation.retained` as expected
live. Further NUnit static scrubbing will not change that result.

## Environment

| Host | Status | Notes |
|------|--------|-------|
| Revit 2023 | Live | net48; async and the full Task 1 matrix pass after the off-UI Run fix |
| Revit 2026 | Live | net8, stands in for the plan's Revit 2025 ALC row |
| Revit 2025 | Not run | Substituted by 2026 |
| Revit 2027 | Not run | Not installed |
| AutoCAD / Civil 3D 2026 | Live | Runner smoke passed |

## Results

Civil 3D 2026 smoke: `samples/DevTools.NUnit.Civil3D.SampleTests` with
`--host Civil3D --version 2026 --host-launch`. `Arithmetic_runs_inside_civil3d_host`
passed (`EXIT=0`) with `acad-version=25.1.0.0`, `host-pid=38048`,
`process-name=acad`, `civil-assembly=AeccUiWindows`
(`civil3d-2026-arithmetic-smoke.txt`).

Revit 2023:

- Async probe (`AsyncTest_Completes`, `AsyncLifecycle_TestCompletes`): 2 passed (`2023-async-probe-out.txt`).
- Full Task 1 matrix: 27 passed, 2 skipped, 1 inconclusive, 1 deliberate error (`2023-task1-full-out.txt`). Includes the async fixtures, so the earlier UI deadlock is closed.
- Generation two on the same host without restart: `generation-marker=generation-two` with a new `generation_id` (`2023-gen2-marker-out.json`).

Revit 2026:

- The generation one to two marker switch works, and the diagnostic now arrives on the generation-two response (`2026-alc-gen1.json`, `2026-alc-gen2.json`, plus retry, stream-load, and host-share packs `2026-alc-gen*-hostshare.txt`, `2026-alc-gen*-hostshare2.txt`).
- `DevTools.NUnit.Host.Tests` prove isolation and host-share; they do not require `generation.unloaded` after Run.
- After host-sharing `nunit.framework`, live Revit 2026 still reports `generation.retained`. The scan shrank from `[Runtime, Fixtures, nunit.framework]` to `[Runtime, Fixtures]`, so NUnit is no longer in the collectible ALC and no scanner-visible host static remains.

## Fixes from this work

1. Off-UI `nunit/run`: `NUnitRequestHandler` no longer marshals the whole NUnit session onto Revit Idling through `IHostContextExecutor`. Discover still marshals; Run uses `Task.Run` with `ExecutionGuardMode.Suppress` on the async flow.
2. Unload diagnostic timing: `NUnitRuntimeManager` releases obsolete generations before enriching the response, so `runtime_diagnostic` appears on the switching response rather than the next request.
3. Host and unit tests cover off-UI Run. Unload after Run is not asserted by unit tests.

## Why live unload stays retained

Collectible ALC unload is cooperative
([Microsoft: assembly unloadability](https://learn.microsoft.com/dotnet/standard/assembly/unloadability)).
`AssemblyLoadContext.Unload()` only starts it. It finishes when no thread has a
collectible method on its stack and no outside strong reference or strong
`GCHandle` points at a collectible assembly, type, or instance (stack and JIT
locals, statics, `AsyncLocal` / ExecutionContext, `RegisteredWaitHandle`, fields
on the ALC subclass). net48 AppDomain unload, by contrast, is a forced abort.

Unit tests run in a quiet process and can still collect a generation after
discover and dispose. Live `nunit/run` uses `Task.Run` on Revit's persistent
ThreadPool, and NUnit `RunAsync`, timeouts, and async fixtures hop more pool
threads. NUnit 4 stores `TestExecutionContext` in `AsyncLocal`, and idle pool
workers keep the previous ExecutionContext. That pins `[Runtime, Fixtures]`.

| Layer | Result |
|-------|--------|
| Isolation | Proven live: new `generation_id` and generation-two IL on the same PID |
| Host-share versioned `nunit.framework` (not Dynamo 3.x) | Works: unit tests keep 4.6 and stub 3.14 distinct; live scan dropped `nunit.framework` from the collectible ALC |
| NUnit `TestMetadataCache` / `MethodInfoCache` / `AsyncLocal` | Real pins, not chased in product code; reflection clears only quieted unit processes |
| Live `generation.unloaded` | Not achieved; still `[Runtime, Fixtures]` after host-share |

Options considered:

| Approach | Closes it? | Notes |
|----------|------------|-------|
| More NUnit cache and `AsyncLocal` clears | No | Enough for unit tests; the live pin is outside the scanner |
| Flood the ThreadPool to overwrite ExecutionContext | Partly, unsafe | Might confirm the theory; cannot cover the UI thread; takes Revit pool threads |
| Host-share Runtime too (fixtures-only collectible ALC) | Smaller surface, same pin | Fixture `Type` still lives in NUnit `AsyncLocal` on pool threads |
| Dedicated run thread with Join | Better, not closed | NUnit internals still use the ThreadPool |
| `!gcroot` on the live `LoaderAllocator` | Diagnostic only | Names the root; does not unload |
| Out-of-process NUnit Engine | Outside the ADR | Tests must run inside the Autodesk host |
| Accept `generation.retained`; require isolation | Chosen | Matches the net48 model (ADR 0016 §5) and cooperative unload; landed as the ADR 0016 §4 amendment |

Kept after accepting the gap: host-shared versioned `nunit.framework`, off-UI
`Task.Run` for `nunit/run`, stream-loaded shadow assemblies, shared
SRM/Immutable and host NuGet prefixes on modern TFMs, and releasing obsolete
sessions before enrich. Removed: the retain-root scanner, dedicated verify
thread, `ExecutionContext.SuppressFlow`, NUnit private-cache reflection, session
field nulling, MethodInfo-avoiding source lookup, and the 40-cycle verify.

## Task 8 close criteria

| Plan item | Status |
|-----------|--------|
| 2023 Dynamo and `nunit.framework` inventory | Done (earlier pack) |
| 2023 Task 1 discover and run, including async | Done |
| 2023 generation two on the same PID | Done |
| 2025/2026 ALC unload | Switch works; live stays `generation.retained` |
| 2027 live | Blocked: not installed |
| AutoCAD smoke | Done (Civil 3D 2026, PID 38048) |
| `dotnet test` Revit API smoke | Done (earlier pack; filter was still imperfect) |

P1 (the thin MTP package `DevTools.NUnit`) was unblocked by this result.
