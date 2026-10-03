# Test Matrix

**Read this before adding tests, raising coverage %, or refactoring Execution / MCP / Daemon / Ipc.**
Gaps and untestable limits below are the source of truth. Do not invent Skip gates,
reset pythonnet, swap `HostDispatcher`, spawn a second testhost on a live `bin/`,
or treat merged HTML **Total %** as the product gate.

Relative coverage of **this repo's** `tests/` — by product area, not by file.
Host Autodesk years / TFMs: [`build-matrix.md`](build-matrix.md).
Client pytest (`RevitDevTool.PyTest`) is a separate repo; only host-side bridge
code here is in scope.

Passing unit tests does not prove live host behavior, daemon reload, or full
year-matrix packaging.

## Current gaps (2026-10-03)

Out-of-host **≥80% line** target: Daemon (includes `Daemon/Mcp/`), Execution,
**`DevTools.Mcp.Catalog`**, Ipc, Settings, Logging, Telemetry, Utilities,
FileMetadata.Core.
**Not in that gate:** Hosting.Revit/Acad, Presentation/UI, RevitDevTool, AcadDevTool, net48 `*.NetFramework.Tests`.

**MCP test projects:** `tests/DevTools.Mcp.*.Tests` names are **legacy** (pre-merge
layout). They reference **`DevTools.Daemon`** + **`DevTools.Mcp.Catalog`**; there is
no `source/DevTools.Mcp.Core` / `.Server` / `.Client` / `.Adapter` project anymore.

| Gap | Why it is still open | What agents must not do |
|-----|----------------------|-------------------------|
| **`DevTools.Execution` coverage** | Closed by [0034](../decisions/0034-execution-mstest-sdk-scoped-tests.md) / [0035](../decisions/0035-mstest-sdk-repo-tests.md): in-repo testhosts are MSTest.Sdk. Collect `--coverage`, not `--coverlet`. | Use `--coverage --coverage-output-format cobertura --coverage-settings coverage.xml`. Do not add Coverlet. Do not treat old HTML Execution **1.5%** as the suite. |
| **`DevTools.Ipc` ~21% merged** | No dedicated `*.Tests` project. Merge only sees `HostPipeName`. `BridgeMessage` / pytest framing is exercised from Execution + `Testing.Transport`. | Do not add a fake Ipc test project “for %”. Cover Ipc via Execution/Transport contract tests, or a real Ipc test project if the wire changes. |
| **Catalog sample / live-pipe Skip (~9)** | Optional `McpToolsetDemo` / `RevitMcpToolSet` DLLs, pixi bind, live `DevToolsMcp_*`. | Skip, do not Fail. Do not fake packed layouts. |
| **pythonnet one engine / process** | Uninitialized serializer/debugger Skip (`Assert.Inconclusive`) if `PythonEngine.IsInitialized`. pythonnet lives in `DevTools.Execution.Python.Tests` (and pytest-run tests in `Pytest.Tests` may init their own process). | Do not `PythonEngine.Shutdown`. Do not put uninitialized-engine facts in Pytest.Tests. |
| **`HostDispatcher` process-static** | `HostUiHelperTests` / file-change orchestrator Skip if dispatcher already set. | Do not clear the static. Headless path is inline when dispatcher is null. |
| **Testing.Abstractions 79.9%** | 0.1% under 80% on the last owned run. | Not a product bug. Do not dump duplicate “coverage boost” files for 0.1%. |
| **In-host / packaging** | Live `execute_*`, in-host `PytestRunner.py`, ILRepack year matrix, daemon hot-reload. | `mcp-integration-test.md` / host pytest. Out of headless CI. |
| **Pip embed zip vs pixi default** | Pip facts still download python.org embed. Product default is `%AppData%/RevitDevTool/pixi-env`. | Do not re-gate pixi behind `RUN_PIXI_SMOKE`. Skip pixi only if download throws. |

HTML (gitignored): `artifacts/coverage-html/index.html`. Merged **Total 40.3%** is misleading (transitive 0% + missing Execution).

### Last MCP coverage snapshot (Microsoft.Testing `--coverage`, 2026-10-03)

Sequential `dotnet run … -f net10.0-windows -- --coverage --coverage-output-format cobertura --coverage-settings <repo>/tests/mstest-coverage.xml`
(one testhost at a time; artifacts under `artifacts/coverage-mcp-20261003/`). **Merged** line % = union of
instrumented `(file, line)` hits across all seven MCP testhosts below (not Coverlet “owned module”).

| Product assembly | Merged line % (MCP suite) | Gate (≥80%) |
|------------------|---------------------------|-------------|
| `DevTools.Mcp.Catalog` | **83.4%** (2352 / 2820 lines) | ✅ |
| `DevTools.Daemon` (incl. `Daemon/Mcp/`) | **84.2%** (6384 / 7582 lines) | ✅ |

| Test project | Pass | Skip | Fail | Single-run line % (primary assembly) |
|--------------|------|------|------|--------------------------------------|
| `DevTools.Mcp.Catalog.Tests` | 124 | 4 | 0 | Catalog **50.8%** |
| `DevTools.Mcp.Core.Tests` | 38 | 0 | 0 | Catalog 3.5% |
| `DevTools.Mcp.Client.Tests` | 23 | 0 | 0 | Daemon 7.0% |
| `DevTools.Mcp.Server.Tests` | 58 | 0 | 0 | Daemon **49.5%** |
| `DevTools.Mcp.Adapter.Tests` | 17 | 0 | 1 | Catalog 35.9% |
| `DevTools.Execution.Mcp.Tests` | 29 | 0 | 0 | `DevTools.Execution` 3.0% (scoped; whole Execution not gated here) |
| `DevTools.Daemon.Tests` | 88 | 0 | 0 | Daemon **67.0%** |

Adapter failure: `LiveHost_ExecuteCsharp_SyncAndTasksOptIn` — live `DevToolsMcp_*` host without `tasks/get` (Skip when no pipe; fails when an old host is connected). Coverage still collected.

Non-MCP rows below are still the **2026-09-04 Coverlet** snapshot until re-run.

| Product assembly | Line % | Test project (2026-09-04) |
|------------------|--------|-------------------------|
| `DevTools.Execution` (full) | **not in last merge** | scoped MSTest.Sdk projects + `--coverage` |
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
or fake packed layouts just to raise coverage %.

| Limit | What tests do | What not to do |
|-------|----------------|----------------|
| **pythonnet: one engine per process** | Uninitialized serializer/debugger Inconclusive if `PythonEngine.IsInitialized`. pythonnet lives in `Python.Tests` / Catalog; Pytest.Tests may init in its own process. | Do not `PythonEngine.Shutdown` / re-init to force the uninitialized path. |
| **`HostUiHelper.HostDispatcher` is process-static** | `HostUiHelperTests` and `ExecutionOrchestratorFileChangeTests` Skip if a dispatcher is already set. Inline dispatch when dispatcher is null is the headless path. | Do not clear/replace the static dispatcher from tests. This is not “needs Revit.exe”. |
| **Optional artifacts / live pipe** | Sample `McpToolsetDemo` / `RevitMcpToolSet` DLLs, live `DevToolsMcp_*` pipe, ILRepack host layout, pythonnet bind failure on a given pixi: **Skip** with `OptionalArtifact` / packaging hints. | Do not Fail CI when samples or Revit are absent. Do not treat Skip as coverage debt to hack around. |
| **coverlet.MTP is gone** | In-repo testhosts use MSTest.Sdk ([0035](../decisions/0035-mstest-sdk-repo-tests.md)). net48 has no collector. | Do not add `coverlet.MTP`, `coverlet.collector`, or VSTest collectors. |
| **Two testhosts on the same `bin/`** | A second `dotnet run` on the same project can fail in seconds (`MSB3027`). That is not a hung test. | Do not spawn a second coverage run on a live testhost. Wait for the owner, or omit that project. |
| **Daemon desktop is WPF** | STA `System.Windows.Application` session (`WpfSession`). HandyControl skin and theme from `DevTools.UI`, merged on the test `Application`. | Do not start a MewUI or Direct2D session. A headless box without a WPF dispatcher is a real env gap. |
| **In-host product** | `execute_*` on a live Revit/AutoCAD thread, `PytestRunner.py` inside the host, ILRepack year matrix, daemon hot-reload. | Out of `tests/` CI. Use `mcp-integration-test.md` / host pytest. |
| **Pip embed vs pixi product default** | Pip facts still download a python.org embed zip. Product default Python is `%AppData%/RevitDevTool/pixi-env`. Pixi CLI is asserted via `PixiInstaller.SetupPixiAsync` (Skip only if download throws). | Do not re-gate pixi/pip behind `RUN_PIXI_SMOKE` / host-absent Skip. |

**Coverage reading:** `--coverage-settings coverage.xml` includes `DevTools.*` and
excludes `*.Tests`. **Owned-module row is the truth**; Total % is misleading.
Run coverage **one project at a time**. If instrumentation fails immediately, stop —
do not wait for `--timeout`.

MCP merged snapshot: **2026-10-03** (`--coverage`). Other assemblies in the table below: **2026-09-04 Coverlet** until re-run.

## Summary

| Area | Relative coverage | Notes |
|------|-------------------|-------|
| MCP Catalog + Daemon `Mcp/` + legacy `Mcp.*.Tests` | **83–84% merged line** (2026-10-03 `--coverage`) | Daemon desktop tests are WPF STA. Adapter live-pipe test fails if a connected host lacks Tasks; headless conformance still runs. |
| Execution | Tests exist in scoped MSTest.Sdk projects; measure with `--coverage` | Independent of Revit.exe — mock `IHostContextExecutor`. See Current gaps. |
| Ipc | **Low in merge (~21%)** | No dedicated test project; framing covered via Execution / Testing.Transport. |
| NUnit / Testing | **Medium–high** | In-host product; in-repo testhosts are MSTest.Sdk (out of process). |
| Settings / Logging / Telemetry / Utilities / FileMetadata.Core / Hosting | **≥80% line** | Hosting.Revit/Acad remain out of the out-of-host gate. |

---

## Line coverage (how to measure)

Do **not** treat a coverage HTML merge as a replacement for this matrix. Snapshot and gate list: **Current gaps** above.

Daemon desktop is HandyControl WPF, including `hc:NotifyIcon` (ADR 0032). Do not treat it as MewUI.
Do **not** Skip pixi/pip because a host app is absent.

`IHostContextExecutor` in Execution tests = inline mock, not Revit. Host adapters live in
`RevitDevTool` / `AcadDevTool`, not in `DevTools.Execution`.

Need a collector **only** when you want line/branch numbers. This repo is MTP
(`dotnet run --project tests/<proj>/<proj>.csproj`). VSTest collectors do not apply.

In-repo testhosts are MSTest.Sdk ([0035](../decisions/0035-mstest-sdk-repo-tests.md)).
net10 projects set `UseMicrosoftCodeCoverage=true` (SDK Default profile).
net48 `*.NetFramework.Tests` have no collector. Settings:
`tests/mstest-coverage.xml` (copied as `coverage.xml`). Collection is **opt-in
per run**, not a CI gate.

| Tool | Flag | Use when |
|------|------|----------|
| **`Microsoft.Testing.Extensions.CodeCoverage`** | `--coverage` | net10 testhosts. Cobertura under MTP `--results-directory`. |
| `coverlet.MTP` / `--coverlet` | — | **No.** Removed from `tests/`. |
| `coverlet.collector` / `coverlet.msbuild` | `--collect` | **No.** VSTest-only. |

```powershell
dotnet run --project tests/DevTools.Execution.CSharp.Tests/DevTools.Execution.CSharp.Tests.csproj -c Debug -f net10.0-windows -- --coverage --coverage-output-format cobertura --coverage-settings tests/mstest-coverage.xml
```

Sequential merge (one project at a time; shared `--results-directory`):

```powershell
$out = "$PWD\artifacts\coverage"
# then for each out-of-host net10 *.Tests.csproj:
dotnet run --project tests/<proj>/<proj>.csproj -c Debug -f net10.0-windows -- --coverage --results-directory $out --coverage-output-format cobertura --coverage-settings tests/mstest-coverage.xml
```

HTML: ReportGenerator on `artifacts/coverage/coverage.cobertura*.xml` (gitignored). Check **Current gaps** before claiming a new Total %.

Include filter is `DevTools.*` (see `coverage.xml`). If MSBuild `MSB3027` on `tests/*/bin`, **stop that project** — testhost lock, not a hung test.

---

## MCP

Production assemblies: **`DevTools.Mcp.Catalog`** (host pipe + store + `DevTools.Mcp.Core.*`
namespaces under `Core/`) and **`DevTools.Daemon/Mcp/`** (fixed tools, `ProcessSessions`,
`search_dynamic` / `invoke_dynamic`). Test project names predate the merge.

Optional fixtures (`McpToolsetDemo`, `RevitMcpToolSet`, pixi, live `DevToolsMcp_Revit_*`) **Skip** — they must not fail a headless run.

| Project | Scope (maps to code today) |
|---------|----------------------------|
| `tests/DevTools.Mcp.Core.Tests` | `Catalog/Core/` protocol models, `CatalogId`, JSON contracts |
| `tests/DevTools.Mcp.Catalog.Tests` | Store, parsers, ALC/isolation (pythonnet only here), built-in registry |
| `tests/DevTools.Mcp.Adapter.Tests` | Host SDK `McpServer` harness, named-pipe conformance |
| `tests/DevTools.Mcp.Client.Tests` | `McpClientPassthrough`, pipe scanner, SDK stream transport |
| `tests/DevTools.Mcp.Server.Tests` | Daemon `SearchTool` / `InvokeTool`, MRTR harness, task selection |
| `tests/DevTools.Execution.Mcp.Tests` | `IMcpSource` backends (`BuiltInSource`, `DotnetSource`, …), built-in tools |
| `tests/DevTools.Daemon.Tests` | `ServerHostBuilder` composition, control JSON, loopback auth callback. Headless: tests pass a no-op URL opener |

`AuthBrowser` opens the sign-in URL with `Process.Start` and `UseShellExecute = true`. Production login still does that. Tests pass `openUrl: _ => { }` and never start a process. `about:blank` has no registered application, so the real opener shows a modal "Pick an app" dialog and does not return until it is closed.

### Well covered

- **Protocol & models** — SDK session framing, host `McpServer` routing, conformance subset
- **Daemon composition** — server builder, fixed tools, `search_dynamic` / `invoke_dynamic` harness; WPF desktop session (STA) for tray/control surfaces
- **Catalog & encoding** — host catalog merge, list/response encoders, dynamic tool contracts
- **Built-in registry** — `BuiltInMcpRegistryProvider` name/bindings; `DotnetMcpRegistryProvider` empty/missing paths (sample DLL Skip)
- **Toolset discovery** — .NET + Python parsers, argument binding, result/MRTR mapping, ALC bridges
- **SDK integration** — stream transport contracts, in-process named-pipe round-trip (mock host)
- **Connection tracking** — `McpConnectTracker` headless via `HostUiHelper.RunOnMainThread` inline when no dispatcher (`DevTools.Execution.Mcp.Tests` / `Services.Tests`)

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

`tests/DevTools.NUnit.Host.Tests` — `[assembly: DoNotParallelize]` because
`TestingRunTraceScope` mutates process-wide `Trace.Listeners`.

### Well covered

- **Trace/Debug capture** — `TestingRunTraceScope` snapshots/restores listeners and re-registers before `CompleteCase` (`DevTools.Testing.Abstractions.Tests`). Runtime e2e via `NUnitRuntimeSessionOutputTests` asserts Console + Trace + Debug markers (`spike-*-marker`) including after a full-assembly warmup (ADR 0017).
- **Session / isolation / packaging targets** — in-process Host.Tests plus Runtime/MTP projects.

### Partial / fragile

- **Packed host output** — `HostPackagingOwnershipTests.Packed_host_output_*` **Skips** unless the layout is actually ILRepacked (unpackaged Debug still copies `NUnitRuntime` and leaves `DevTools.Testing.Host.dll` loose). Build host with ILRepack, or set `DEVTOOLS_PACKED_HOST_OUTPUT`.

In-repo testhosts are MTP executables (`tests/Directory.Build.props`). Run
`dotnet run --project tests/<project>/<project>.csproj` (optional
`-- --filter ClassName`) or `dotnet test --project` from the repo root (root
`global.json` is MTP). Product samples use the same root runner.
`samples/ricaun.NUnit.SampleTests` is the only VSTest project — its folder
`global.json` sets `"runner": "VSTest"`; `cd` there. Comparison only. Do not
force `--progress off`.

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

Scoped MSTest.Sdk projects ([0034](../decisions/0034-execution-mstest-sdk-scoped-tests.md)):
`CSharp`, `FSharp`, `Python`, `IronPython`, `Pytest`, `Mcp`, `Services` plus
`Tests.Shared`. Pixi/pip **run** in Python.Tests / Pytest.Tests (download allowed).
Skip (`Assert.Inconclusive`) only on download/bind failure.

pythonnet init belongs in **Python.Tests** (Pytest.Tests may init in its own
process for run/prepare). Uninitialized-engine facts stay in Python.Tests.

Measure Execution (and other net10 testhosts) with `--coverage`.

### Well covered

- **Python environment** — host DLL / version select, uv attach + sidecar stdlib,
  pixi partition, `IPythonPackageStore` DI, pip-list JSON parse; Parser installed-state
  via list JSON; pixi CLI (`--version` / `--help`) after `SetupPixiAsync`
- **Execution guard** — `ExecutionGuardContext` ambient mode / rollback summary
- **MCP dispatch** — `IMcpSource.InvokeToolAsync` / resource read; built-in and dotnet backends (`DevTools.Execution.Mcp.Tests`)
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
| MCP tests | `dotnet run --project tests/DevTools.Mcp.Catalog.Tests/DevTools.Mcp.Catalog.Tests.csproj` (also Core, Server, Client, Adapter, `DevTools.Execution.Mcp.Tests`) |
| .NET tests | `dotnet run --project tests/<project>/<project>.csproj` |
| Python parser | `scripts/test-python.ps1` |
| Live MCP | `docs/agents/mcp-integration-test.md` |

## Reporting

State the **feature gap** (e.g. "pytest bridge: no in-host `PytestRunner.py` run"), missing
build artifact, or environment (pixi, host PID). Do not claim full platform verification
when only compile or a single project passed. Optional fixtures must **Skip**, not Fail.
Do not treat a low coverage % as a product bug without checking **Current gaps** first.
Do not add tests or features that assume Daemon is WPF, Execution needs Revit.exe, or pixi is opt-in `RUN_PIXI_SMOKE`.
