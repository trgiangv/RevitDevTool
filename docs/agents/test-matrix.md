# Test Matrix

**Read this before adding tests, raising Coverlet %, or refactoring Execution / MCP / Daemon / Ipc.**
Gaps and untestable limits below are the source of truth. Do not invent Skip gates,
reset pythonnet, swap `HostDispatcher`, spawn a second Coverlet on a live testhost,
or treat merged HTML **Total %** as the product gate.

Relative coverage of **this repo's** `tests/` — by product area, not by file.
Host Autodesk years / TFMs: [`build-matrix.md`](build-matrix.md).
Client pytest (`RevitDevTool.PyTest`) is a separate repo; only host-side bridge
code here is in scope.

Passing unit tests does not prove live host behavior, daemon reload, or full
year-matrix packaging.

## Current gaps (2026-09-04)

Out-of-host **≥80% line** target: Daemon, Execution, MCP Core/Catalog/Server/Client,
Ipc, Settings, Logging, Telemetry, Utilities, FileMetadata.Core.
**Not in that gate:** Adapter, Hosting.Revit/Acad, Presentation/UI, RevitDevTool, AcadDevTool, net48 `*.NetFramework.Tests`.

| Gap | Why it is still open | What agents must not do |
|-----|----------------------|-------------------------|
| **`DevTools.Execution` Coverlet not in the HTML merge** | MTP Coverlet instruments DLLs in `tests/DevTools.Execution.Tests/bin/`. A second testhost (`--coverlet`) holds those files; the next run fails in seconds (`MSB3027` / `[Coverlet] Coverage instrumentation failed`). Tests exist (pixi, pipe, compilers, pytest bridge). Last merge omitted this assembly. | Do not spawn another Coverlet while a testhost is on that `bin/`. Do not kill the owner. Wait, then **one** `dotnet run --no-build … --coverlet`. Do not treat HTML Execution **1.5%** as the suite (that is Catalog touching `PythonMcpToolBackend`). |
| **`DevTools.Ipc` ~21% merged** | No dedicated `*.Tests` project. Merge only sees `HostPipeName`. `BridgeMessage` / pytest framing is exercised from Execution + `Testing.Transport`. | Do not add a fake Ipc test project “for %”. Cover Ipc via Execution/Transport contract tests, or a real Ipc test project if the wire changes. |
| **Catalog sample / live-pipe Skip (~9)** | Optional `McpToolsetDemo` / `RevitMcpToolSet` DLLs, pixi bind, live `DevToolsMcp_*`. | Skip, do not Fail. Do not fake packed layouts. |
| **pythonnet one engine / process** | Uninitialized serializer/debugger Skip if `PythonEngine.IsInitialized`. Collection order vs pixi is undefined. | Do not `PythonEngine.Shutdown`. Any fact that calls `EnsurePixiPythonInitializedAsync` / `InitializeAsync` / `PytestDependencyService.PrepareRunAsync` **must** use `[Collection(nameof(PythonRuntimeCollection))]`. Nested collection classes are OK; a class *outside* the collection that inits Python is not. |
| **`HostDispatcher` process-static** | `HostUiHelperTests` / file-change orchestrator Skip if dispatcher already set. | Do not clear the static. Headless path is inline when dispatcher is null. |
| **Testing.Abstractions 79.9%** | 0.1% under 80% on the last owned run. | Not a product bug. Do not dump duplicate “coverage boost” files for 0.1%. |
| **In-host / packaging** | Live `execute_*`, in-host `PytestRunner.py`, ILRepack year matrix, daemon hot-reload. | `mcp-integration-test.md` / host pytest. Out of headless CI. |
| **Pip embed zip vs pixi default** | Pip facts still download python.org embed. Product default is `%AppData%/RevitDevTool/pixi-env`. | Do not re-gate pixi behind `RUN_PIXI_SMOKE`. Skip pixi only if download throws. |

HTML (gitignored): `artifacts/coverage-html/index.html`. Merged **Total 40.3%** is misleading (transitive 0% + missing Execution).

### Last Coverlet snapshot (owned-module line %)

Sequential `dotnet run … --coverlet` 2026-09-04 16:28–16:37 + Daemon retry 16:37. Execution omitted.

| Product assembly | Line % | Test project (this run) |
|------------------|--------|-------------------------|
| `DevTools.Mcp.Core` | 82.9% owned / 94.7% merged | 62 pass |
| `DevTools.Mcp.Catalog` | **88.8%** | 152 pass / 9 skip |
| `DevTools.Mcp.Server` | **91.6%** | 61 pass |
| `DevTools.Mcp.Client` | **90.5%** | 20 pass |
| `DevTools.Daemon` | **80.4%** | 76 pass |
| `DevTools.Execution` | **not measured** | testhost lock |
| `DevTools.Testing.Abstractions` | 79.9% | 57 pass |
| `DevTools.Utilities` | **94.9%** | 18 pass |
| `DevTools.Settings` | **93.3%** | 12 pass |
| `DevTools.Logging` | **95.8%** | 43 pass |
| `DevTools.Telemetry` | **96.0%** | 24 pass |
| `DevTools.FileMetadata.Core` | **100%** | 10 pass |
| `DevTools.Hosting` | **86.9%** | 71 pass (Hosting.Revit/Acad still out of gate) |
| `DevTools.Ipc` | 20.9% merged | no dedicated project |

## Untestable limits (do not workaround)

These are process or environment constraints. Tests **Skip** with a reason, or
the fact is simply out of this repo. Do **not** reset pythonnet, swap dispatchers,
or fake packed layouts just to raise Coverlet %.

| Limit | What tests do | What not to do |
|-------|----------------|----------------|
| **pythonnet: one engine per process** | `PythonJsonSerializerUninitializedTests` / `PythonDebuggerUninitializedTests` Skip if `PythonEngine.IsInitialized`. Pixi/python facts share `[Collection(nameof(PythonRuntimeCollection))]` (no parallelization). Collection order is undefined, so uninitialized facts may Skip after a pixi run. | Do not `PythonEngine.Shutdown` / re-init to force the uninitialized path. |
| **`HostUiHelper.HostDispatcher` is process-static** | `HostUiHelperTests` and `ExecutionOrchestratorFileChangeTests` Skip if a dispatcher is already set. Inline dispatch when dispatcher is null is the headless path. | Do not clear/replace the static dispatcher from tests. This is not “needs Revit.exe”. |
| **Optional artifacts / live pipe** | Sample `McpToolsetDemo` / `RevitMcpToolSet` DLLs, live `DevToolsMcp_*` pipe, ILRepack host layout, pythonnet bind failure on a given pixi: **Skip** with `OptionalArtifact` / packaging hints. | Do not Fail CI when samples or Revit are absent. Do not treat Skip as coverage debt to hack around. |
| **coverlet.MTP is net8+** | `*.NetFramework.Tests` (net48) have no Coverlet package. | Do not add `coverlet.collector` / VSTest collectors. |
| **Coverlet + a live testhost on the same `bin/`** | MTP Coverlet instruments DLLs in the test output folder. A second `dotnet run` / testhost on the same project fails in seconds (`CS2012` / `MSB3027` / `[Coverlet] Coverage instrumentation failed`). That is not a hung test. | Do not spawn a second Coverlet, do not kill the other testhost, do not wait 25m. Wait for the owner to finish, or omit that project from the merge. |
| **Daemon desktop is MewUI** | STA `Application.Create().UseWin32().UseDirect2D()` session (`MewUiSession`). Not WPF / FlaUI / WPF-MCP. | Do not Skip Daemon UI as “WPF tray”. A headless box without Win32/Direct2D is a real env gap. |
| **In-host product** | `execute_*` on a live Revit/AutoCAD thread, `PytestRunner.py` inside the host, ILRepack year matrix, daemon hot-reload. | Out of `tests/` CI. Use `mcp-integration-test.md` / host pytest. |
| **Pip embed vs pixi product default** | Pip facts still download a python.org embed zip. Product default Python is `%AppData%/RevitDevTool/pixi-env`. Pixi CLI is asserted via `PixiInstaller.SetupPixiAsync` (Skip only if download throws). | Do not re-gate pixi/pip behind `RUN_PIXI_SMOKE` / host-absent Skip. |

**Coverlet reading:** `--coverlet-include '[DevTools.*]*'` also instruments unused
transitive `DevTools.*` at 0%. **Owned-module row is the truth**; Total % is
misleading. Parallel `dotnet run` can lock `DevTools.Mcp.Catalog.dll` — run
Coverlet **one project at a time**. If instrumentation fails immediately, stop —
do not wait for `--timeout`.

## Summary

| Area | Relative coverage | Notes |
|------|-------------------|-------|
| MCP Core/Catalog/Server/Client + Daemon | **≥80% line** (last snapshot) | Daemon is **MewUI**, not WPF. Adapter/live pipe is host-process (out of gate). |
| Execution | Tests exist; **Coverlet not in last merge** | Independent of Revit.exe — mock `IHostContextExecutor`. See Current gaps. |
| Ipc | **Low in merge (~21%)** | No dedicated test project; framing covered via Execution / Testing.Transport. |
| NUnit / Testing | **Medium–high** | In-host product; xUnit harness is out of process. |
| Settings / Logging / Telemetry / Utilities / FileMetadata.Core / Hosting | **≥80% line** | Hosting.Revit/Acad remain out of the out-of-host gate. |

---

## Line coverage (how to measure)

Do **not** treat Coverlet as a replacement for this matrix. Snapshot and gate list: **Current gaps** above.

Do **not** call Daemon “WPF tray” — desktop is MewUI Direct2D + `H.NotifyIcon.Core` (ADR 0032).
Do **not** Skip pixi/pip because a host app is absent.

`IHostContextExecutor` in Execution tests = inline mock, not Revit. Host adapters live in
`RevitDevTool` / `AcadDevTool`, not in `DevTools.Execution`.

Need a collector **only** when you want line/branch numbers. This repo is MTP
(`dotnet run --project tests/<proj>/<proj>.csproj`). VSTest collectors do not apply.

`coverlet.MTP` **10.0.1** is referenced centrally for every `*.Tests` executable except
`*.NetFramework.Tests` (net48). Shared settings: `tests/testconfig.json`. Collection is
**opt-in per run** (`--coverlet`), not a CI gate.

| Tool | Flag | Use when |
|------|------|----------|
| **`coverlet.MTP`** | `--coverlet` | Wired. Cobertura + json under MTP `--results-directory`. |
| `Microsoft.Testing.Extensions.CodeCoverage` | `--coverage` | Official Microsoft collector — do not add alongside Coverlet. |
| `coverlet.collector` / `coverlet.msbuild` | `--collect` | **No.** VSTest-only. |

```powershell
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -c Debug -- --coverlet
```

Sequential merge (one project at a time; shared `--results-directory`):

```powershell
$out = "$PWD\artifacts\coverage"
# then for each out-of-host *.Tests.csproj:
dotnet run --project tests/<proj>/<proj>.csproj -c Debug -- --coverlet --results-directory $out --coverlet-file-prefix <proj>
```

HTML: ReportGenerator on `artifacts/coverage/coverage.cobertura*.xml` (gitignored). Check **Current gaps** before claiming a new Total %.

Include filter is `[DevTools.*]*`. Threshold fail is MTP exit `14`. If Coverlet prints `instrumentation failed` or MSBuild `MSB3027` on `tests/*/bin`, **stop that project** — testhost lock, not a hung test.

---

## MCP

Split by source module. Optional fixtures (`McpToolsetDemo`, `RevitMcpToolSet`, pixi, live `DevToolsMcp_Revit_*`) **Skip** — they must not fail a headless run.

| Project | Scope |
|---------|--------|
| `tests/DevTools.Mcp.Core.Tests` | Contracts, protocol models, list/invocation JSON |
| `tests/DevTools.Mcp.Catalog.Tests` | Store, parsers, invoker, ALC/isolation (pythonnet only here), built-in registry |
| `tests/DevTools.Mcp.Adapter.Tests` | Host wire, handler, JSON-RPC, conformance |
| `tests/DevTools.Mcp.Client.Tests` | Passthrough surface, pipe scanner, SDK stream/named-pipe |
| `tests/DevTools.Mcp.Server.Tests` | `search_dynamic` / `invoke_dynamic` harness, daemon options |
| `tests/DevTools.Daemon.Tests` | `ServerHostBuilder` composition, control JSON |

### Well covered

- **Protocol & models** — JSON-RPC framing, host handler routing, conformance subset
- **Daemon composition** — server builder, fixed tools, `search_dynamic` / `invoke_dynamic` harness; MewUI desktop session (STA Direct2D) for tray/control surfaces
- **Catalog & encoding** — host catalog merge, list/response encoders, dynamic tool contracts
- **Built-in registry** — `BuiltInMcpRegistryProvider` name/bindings; `DotnetMcpRegistryProvider` empty/missing paths (sample DLL Skip)
- **Toolset discovery** — .NET + Python parsers, argument binding, result/MRTR mapping, ALC bridges
- **SDK integration** — stream transport contracts, in-process named-pipe round-trip (mock host)
- **Connection tracking** — `McpConnectState` headless via `HostUiHelper.RunOnMainThread` inline when no dispatcher (`DevTools.Execution.Tests`)

### Partial / fragile

- **Parser integration** — Skips unless sample toolsets + pixi env are present; sample metadata can drift during SDK migration
- **Named pipe to real host** — `HostTasksLiveIntegrationTests` Skips without a live pipe; full daemon → deployed host → tool invoke is not automated
- **ILRepack host layout** — unpackaged Debug is not treated as packed proof (`IsRepackedHostLayout`)

### Not in headless CI

- **Live host** — `execute_*` success on a real Revit/AutoCAD thread — manual checklist (`mcp-integration-test.md`)
- **Packaging & reload** — ILRepack year matrix, daemon hot-reload, shared-runtime layout (some packaging asserts, not full matrix)
- **End-to-end toolset invoke** — catalog discovery → host dispatch → Revit API (no single CI test)

**Prerequisites (optional, else Skip):** build `samples/McpToolsetDemo` and/or `samples/RevitMcpToolSet`; pixi env at `%APPDATA%\RevitDevTool\pixi-env` (`scripts/test-python.ps1`).

---

## NUnit host

`tests/DevTools.NUnit.Host.Tests` — parallelization off (`CollectionBehavior`) because
`TestingRunTraceScope` mutates process-wide `Trace.Listeners`.

### Well covered

- **Trace/Debug capture** — `TestingRunTraceScope` snapshots/restores listeners and re-registers before `CompleteCase` (`DevTools.Testing.Abstractions.Tests`). Runtime e2e via `NUnitRuntimeSessionOutputTests` asserts Console + Trace + Debug markers (`spike-*-marker`) including after a full-assembly warmup (ADR 0017).
- **Session / isolation / packaging targets** — in-process Host.Tests plus Runtime/MTP projects.

### Partial / fragile

- **Packed host output** — `HostPackagingOwnershipTests.Packed_host_output_*` **Skips** unless the layout is actually ILRepacked (unpackaged Debug still copies `NUnitRuntime` and leaves `DevTools.Testing.Host.dll` loose). Build host with ILRepack, or set `DEVTOOLS_PACKED_HOST_OUTPUT`.

xUnit v3 tests are MTP executables (`tests/Directory.Build.props`). Run
`dotnet run --project tests/<project>/<project>.csproj` (optional
`-- --filter ClassName`). Root `dotnet test` is VSTest-mode and fails on
.NET 10 SDK. Product samples use `dotnet test` from their folder (scoped
MTP `global.json`). `samples/ricaun.NUnit.SampleTests` is the only VSTest
project — comparison only. Do not force `--progress off`.

---

## Pytest bridge

Host pipe (`DevTools_*`) is separate from MCP pipe (`DevToolsMcp_*`).

### Well covered

- **Wire identity** — pipe name format and pytest vs MCP discrimination
- **Framing** — length-prefixed `BridgeMessage` distinct from MCP NDJSON
- **Parse / routing** — `PytestExecutionService.TryParseRunRequest`, `PytestPathResolver`, unknown-method + invalid-params on `PytestRequestHandler` / `IpyTestRequestHandler`, `instance/info`
- **Pipe server** — `DevToolsPipeServer` start/stop/dispose and in-process `instance/info` round-trip (`DevToolsPipeServerTests`; unique pipe names)
- **Progress vs batch** — null `NotificationSender` → no progress callback; set sender → callback created

### Not in headless CI

- **In-host runner** — `PytestRunner.py` / `IpyTestDriver.py` + pixi PEP 723 install (needs Python in host)
- **Cross-process flow** — discover → connect → run → report (covered in client repo, not host CI)

---

## Execution

Unit layer in `tests/DevTools.Execution.Tests` (python runtime / host-attach, pytest bridge, package stores). Pixi/pip **run** in the same process (download allowed). Skip only on download/bind failure.

Any fact that initializes pythonnet **must** be `[Collection(nameof(PythonRuntimeCollection))]`. Last Coverlet merge **did not include** this project (testhost lock) — see Current gaps.

### Well covered

- **Python environment** — host DLL / version select, uv attach + sidecar stdlib,
  pixi partition, `IPythonPackageStore` DI, pip-list JSON parse; Parser installed-state
  via list JSON; pixi CLI (`--version` / `--help`) after `SetupPixiAsync`
- **Execution guard** — `ExecutionGuardContext` ambient mode / rollback summary
- **MCP dispatch** — `McpPrimitiveDispatcher` unsupported mode / success / exception / `InputRequiredException`
- **Built-in tools (headless)** — `open_document` via mock `IDocumentBridge`; `execute_csharp_code` empty + compile-fail; `execute_python_code` empty (before pythonnet init, else Skip)
- **Orchestration load** — `ExecutionOrchestrator.LoadFromPathAsync` no-provider / covered-root skip / watch + `TreeChanged`
- **C# directives** — `#r nuget` + `#load` graph (`CSharpDirectiveParser`)
- **File watcher** — watch / debounce / unwatch / dispose (`FileWatcherService`)
- **Host UI marshal** — `RunOnMainThread` inline when dispatcher is null
- **Assembly isolation** — existing isolation fixtures

### Not in headless CI

- **In-host script success** — compile-and-run on a live Revit/AutoCAD thread (`IHostContextExecutor` in product hosts)
- **Watcher → UI reload** — debounce is unit-tested; execute/reload path is host
- **`ToolInvoke.py` on host** — payload mapping has some coverage; live Python.NET invoke is Catalog/host
- **Pip embed zip** — still downloads python.org embed; not the product pixi-env default (see limits)

`tests/RevitDevTool.PyServer.Tests/` — small Python parser check only.

---

## Verification

| Goal | Command |
|------|---------|
| MCP tests | `dotnet run --project tests/DevTools.Mcp.Core.Tests/DevTools.Mcp.Core.Tests.csproj` (also Catalog, Adapter, Client, Server) |
| .NET tests | `dotnet run --project tests/<project>/<project>.csproj` |
| Python parser | `scripts/test-python.ps1` |
| Live MCP | `docs/agents/mcp-integration-test.md` |

## Reporting

State the **feature gap** (e.g. "pytest bridge: no in-host `PytestRunner.py` run"), missing
build artifact, or environment (pixi, host PID). Do not claim full platform verification
when only compile or a single project passed. Optional fixtures must **Skip**, not Fail.
Do not treat a low Coverlet % as a product bug without checking **Current gaps** first.
Do not add tests or features that assume Daemon is WPF, Execution needs Revit.exe, or pixi is opt-in `RUN_PIXI_SMOKE`.
