# Test Matrix

What this repo's `tests/` cover, by product area, and which gaps are known.
Read Current gaps before adding tests, chasing coverage, or refactoring
Execution, MCP, Daemon, or Ipc: most low numbers and Skips there are expected.

Host years and TFMs: [build-matrix.md](build-matrix.md). The pytest client
(`source/RevitDevTool.PyTest`) tests itself with `pixi run test`; this page
covers the .NET side. Passing unit tests do not prove live host behavior, Daemon
reload, or year-matrix packaging.

The `tests/DevTools.Mcp.*.Tests` names are from before the MCP merge. They test
`DevTools.Daemon` and `DevTools.Mcp.Catalog`; the old `DevTools.Mcp.Core`,
`.Server`, `.Client`, and `.Adapter` source projects no longer exist.

## Writing fast, stable tests

- Expensive setup (downloads, packs, environments) runs once: `[ClassInitialize]`, `[AssemblyInitialize]`, or a static `Lazy<Task>` fixture, and caches on disk when later runs can reuse it. `ParserScriptTests` (cached Python), `PackageConsumerTests` (one pack per class), and the IronPython `Pydevd` helper are the patterns.
- Do not share state through `[DependsOn]`. MSTest only orders tests already in the run, so `--filter` on one test leaves its prerequisite out.
- Test product code, not third-party tools: a test that only proves `pip install` or `dotnet` works belongs nowhere.
- Turn on `[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 0)]` in a project only after five clean runs in a row (`DevTools.Daemon.Tests`, `DevTools.TestRunner.Tests`). Projects with timing-based waits (file watchers) or shared stores stay serial.
- UI tests find elements by AutomationId and act through UI Automation patterns (see HandyControl UI tests below).
- Fixed product timings and outside services are passed in, with the production value as the default: `HostConnections` discovery interval, `PackageVersionChecker` `HttpClient`, `StdioHostedService` input stream, `SentryTelemetry` `ITransport`. Tests pass a short interval, canned JSON, `Stream.Null`, or a capturing transport.
- Wait for an event or state, not for a timeout to expire: stop the run once the awaited status arrives. For an unreachable endpoint use a local server that closes at once (`RejectingServer` in `DevTools.Daemon.Tests`); a refused connect to `127.0.0.1:9` costs about 2 s on Windows. A window a test creates lives on its own thread with a message loop, as a real host UI thread does.

## Current gaps

Process and environment limits, and work still open. A test that hits one of
these Skips with a reason (`Assert.Inconclusive`); it does not fail.

| Gap | Why | What to do |
|-----|-----|------------|
| Coverage collector | In-repo testhosts are MSTest.Sdk ([0034](../decisions/0034-execution-mstest-sdk-scoped-tests.md), [0035](../decisions/0035-mstest-sdk-repo-tests.md)). net48 has no collector. | Collect with `--coverage` (see [Line coverage](#line-coverage)). Coverlet and VSTest collectors (`coverlet.MTP`, `coverlet.collector`, `--collect`) are removed and do not apply. |
| `DevTools.Ipc` low in a merged report | No dedicated `*.Tests` project; the merge only sees `HostPipeName`. `BridgeMessage` and pytest framing are exercised from Execution and `Testing.Transport`. | Cover Ipc through Execution or Transport contract tests, or add a real Ipc test project when the wire changes. |
| Optional artifacts and live pipe | Sample `McpToolsetDemo` / `RevitMcpToolSet` DLLs, a live `DevToolsMcp_*` pipe, ILRepack host layout, and pythonnet bind on a given pixi env may be absent. | Skip with an `OptionalArtifact` or packaging hint. Build the samples to run them (see [MCP](#mcp)). |
| pythonnet: one engine per process | Uninitialized serializer and debugger facts Skip once `PythonEngine.IsInitialized`. pythonnet lives in `DevTools.Execution.Python.Tests` and Catalog; `Pytest.Tests` may init it in its own process. | Keep uninitialized-engine facts in `Python.Tests`. Never call `PythonEngine.Shutdown` to reach the uninitialized path; the engine cannot restart in-process. |
| `HostUiHelper.HostDispatcher` is process-static | `HostUiHelperTests` and `ExecutionOrchestratorFileChangeTests` Skip if a dispatcher is already set. Inline dispatch when it is null is the headless path; this is not a need for Revit.exe. | Leave the static alone in tests; clearing or replacing it breaks other tests in the process. |
| Two testhosts on the same `bin/` | A second `dotnet run` on a project whose testhost is running fails within seconds with `MSB3027`. It is not a hung test. | Run one testhost per project. Never spawn a second run on a live `tests/*/bin`; wait for the owner or leave that project out. |
| Daemon desktop is WPF | STA `System.Windows.Application` session (`WpfSession`) with the HandyControl skin and theme from `DevTools.UI`, merged on the test `Application`, including `hc:NotifyIcon` ([0032](../decisions/0032-daemon-mewui-and-aot.md)). | Test it as WPF. A headless box without a WPF dispatcher is a real environment gap. |
| In-host and packaging | Live `execute_*` on a Revit or AutoCAD thread, in-host `PytestRunner.py`, ILRepack year matrix, Daemon hot-reload. | Out of headless CI. Use [mcp-integration-test.md](mcp-integration-test.md) or host pytest. |
| HandyControl UI tests | ColorPicker tests find parts by AutomationId and act through FlaUI UIA3 patterns (Toggle, SelectionItem, RangeValue, Invoke) on a real window and popup HWND. The eye dropper test checks that the popup stays open and nothing throws; the sampled color depends on the screen and is not asserted. They need a desktop session; not yet seen on a GitHub runner. | Drive UI through AutomationIds and patterns, not coordinate clicks, hit tests, or raised mouse events; that input depends on the desktop and was what failed. If the first CI run fails them, decide with the owner. |
| Test data folder | `tests/TestDataDirectory.cs` (compiled into every `*.Tests` project) sets `REVITDEVTOOL_DATA_DIR` to `%TEMP%\RevitDevTool-TestData`, so `AppUtils.GetApplicationDataPath()` points there instead of `%AppData%\RevitDevTool`. Tests install, update, and remove packages in its pixi-env, uv-env, NuGet cache, and pydevd; the folder persists, so only the first run downloads. | Never point tests at the real `%AppData%\RevitDevTool`; they remove packages. Delete the folder to start clean. |
| MCP client dispose on net48 | A pending named-pipe read ignores cancellation on .NET Framework, so `McpClient.DisposeAsync` waits about 5 s for its read loop. | In net48 tests, dispose the client pipe before the client (`NamedPipeIntegrationTests`). |
| `NUnit.Host.NetFramework.Tests` about 13 s | Each test builds one to four fixture generations; hashing the freshly copied DLLs in `%TEMP%` takes 100–170 ms each, likely on-access antivirus scanning. The net10 `NUnit.Host.Tests` pays the same per generation. | Not a hang. If it grows, build each deterministic generation once per class with a static `Lazy`. |
| Downloads and network | Pixi, uv, pydevd, and package installs download into the test data folder; `ParserScriptTests` caches a python.org embed in `%TEMP%\RevitDevTool-ParserPython`. `PackageVersionChecker` tests use canned PyPI and conda-forge JSON through an `HttpClient`; its NuGet lookup runs `nuget.exe search` and has no offline test. | Download once per class or per folder, not per test. Skip only if the download throws. |

## Line coverage

Out-of-host target: at least 80% line for `DevTools.Daemon` (including
`Daemon/Mcp/`), `DevTools.Execution`, `DevTools.Mcp.Catalog`, `DevTools.Ipc`,
`DevTools.Settings`, `DevTools.Logging`, `DevTools.Telemetry`,
`DevTools.Utilities`, and `DevTools.FileMetadata.Core`. Outside the target:
`Hosting.Revit`, `Hosting.Acad`, Presentation and UI, `RevitDevTool`,
`AcadDevTool`, and net48 `*.NetFramework.Tests`.

Collection is opt-in per run, not a CI step. This repo is MTP
(`dotnet run --project tests/<proj>/<proj>.csproj`). net10 projects set
`UseMicrosoftCodeCoverage=true`; the settings file is `tests/mstest-coverage.xml`
(copied as `coverage.xml`), which includes `DevTools.*` and excludes `*.Tests`.
The collector is `Microsoft.Testing.Extensions.CodeCoverage` (`--coverage`),
writing Cobertura under the MTP `--results-directory`.

```powershell
dotnet run --project tests/DevTools.Execution.CSharp.Tests/DevTools.Execution.CSharp.Tests.csproj -c Debug -f net10.0-windows -- --coverage --coverage-output-format cobertura --coverage-settings tests/mstest-coverage.xml
```

To merge, run each out-of-host net10 `*.Tests.csproj` one at a time into a shared results directory:

```powershell
$out = "$PWD\artifacts\coverage"
# then for each out-of-host net10 *.Tests.csproj:
dotnet run --project tests/<proj>/<proj>.csproj -c Debug -f net10.0-windows -- --coverage --results-directory $out --coverage-output-format cobertura --coverage-settings tests/mstest-coverage.xml
```

Merged line % is the union of instrumented `(file, line)` hits across testhosts.
Run ReportGenerator on `artifacts/coverage/coverage.cobertura*.xml` for HTML
(gitignored). Read the owned-module rows: the merged Total % counts transitive
assemblies at 0% and understates every product. If instrumentation fails
immediately, stop that run instead of waiting for `--timeout`.

## MCP

Production assemblies: `DevTools.Mcp.Catalog` (host pipe, store, and the
`DevTools.Mcp.Core.*` namespaces under `Core/`) and `DevTools.Daemon/Mcp/` (fixed
tools, `HostConnections`, `code_mode` and its catalog search).

| Project | Scope |
|---------|-------|
| `tests/DevTools.Mcp.Core.Tests` | `Catalog/Core/` protocol models, `CatalogId`, JSON contracts |
| `tests/DevTools.Mcp.Catalog.Tests` | Store, parsers, ALC isolation (pythonnet only here), built-in registry |
| `tests/DevTools.Mcp.Adapter.Tests` | Host SDK `McpServer` harness, named-pipe conformance |
| `tests/DevTools.Mcp.Client.Tests` | `McpClientPassthrough`, pipe scanner, SDK stream transport, `ProcessCatalogs` and BM25F scoring |
| `tests/DevTools.Mcp.Server.Tests` | Daemon fixed tool list and prompts, structured output, log filters |
| `tests/DevTools.Execution.Mcp.Tests` | `IMcpSource` backends (`BuiltInSource`, `DotnetSource`, …), built-in tools |
| `tests/DevTools.Daemon.Tests` | `AppHostBuilder` composition, `code_mode` compiler, program API and results, task selection, control JSON, loopback auth callback |

`AuthBrowser` opens the sign-in URL with `Process.Start` and
`UseShellExecute = true`, as production login does. Tests pass
`openUrl: _ => { }` and never start a process, because `about:blank` has no
registered application and the real opener would block on a modal "Pick an app"
dialog.

`LiveHost_ExecuteCsharp_SyncAndTasksOptIn` (Adapter) needs a live
`DevToolsMcp_*` host and is Inconclusive without one. A host built before pipe
sessions copied `RequestHandlers` fails it with "Method 'tasks/get' is not
available"; `HostSessionTasksTests` covers the same path headlessly.

Optional prerequisites (else Skip): build `samples/McpToolsetDemo` and/or
`samples/RevitMcpToolSet`; pixi env at `%APPDATA%\RevitDevTool\pixi-env`
(`scripts/test-pyserver.ps1`).

Well covered:

- Protocol and models: SDK session framing, host `McpServer` routing, conformance subset.
- Daemon composition: server builder, the four fixed tools, `code_mode` programs against an in-memory catalog and a mocked host session; WPF desktop session (STA) for tray and control surfaces.
- Catalog and encoding: host catalog merge, list and response encoders, dynamic tool contracts.
- Built-in registry: `BuiltInMcpRegistry` names and bindings; `DotnetMcpRegistry` empty and missing paths (sample DLL Skip).
- Toolset discovery: .NET and Python parsers, argument binding, result and MRTR mapping, ALC bridges.
- SDK integration: stream transport contracts, in-process named-pipe round trip with a mock host.
- Connection tracking: `McpConnectTracker` headless via `HostUiHelper.RunOnMainThread` inline when there is no dispatcher (`DevTools.Execution.Mcp.Tests`, `Services.Tests`).

Partial:

- Parser integration Skips unless sample toolsets and the pixi env are present; sample metadata can drift during SDK migration.
- `HostTasksLiveIntegrationTests` Skips without a live pipe; Daemon to deployed host to tool invoke is not automated.
- Unpackaged Debug is not treated as packed-layout proof (`IsRepackedHostLayout`).

Not in headless CI:

- `execute_*` success on a real Revit or AutoCAD thread ([mcp-integration-test.md](mcp-integration-test.md)).
- `run_command` loading a built DLL and running its command (mcp-integration-test.md Scenario 12).
- ILRepack year matrix, Daemon hot-reload, shared-runtime layout (some packaging asserts only).
- Catalog discovery to host dispatch to Revit API in one test.

## NUnit host

`tests/DevTools.NUnit.Host.Tests` sets `[assembly: DoNotParallelize]` because
`TestingRunTraceScope` mutates the process-wide `Trace.Listeners`.

Well covered:

- Trace and Debug capture: `TestingRunTraceScope` snapshots and restores listeners and re-registers before `CompleteCase` (`DevTools.Testing.Abstractions.Tests`). `NUnitRuntimeSessionOutputTests` asserts Console, Trace, and Debug markers (`spike-*-marker`), including after a full-assembly warmup ([0017](../decisions/0017-nunit-host-test-output-routing.md)).
- Session, isolation, and packaging targets: in-process Host.Tests plus the Runtime and MTP projects.

Partial:

- `HostPackagingOwnershipTests.Packed_host_output_*` Skips unless the layout is ILRepacked (unpackaged Debug leaves `DevTools.Testing.Host.dll` loose). Build the host with ILRepack, or set `DEVTOOLS_PACKED_HOST_OUTPUT`.

In-repo testhosts are MTP executables (`tests/Directory.Build.props`). Run
`dotnet run --project tests/<project>/<project>.csproj` (optional
`-- --filter ClassName`), or `dotnet test --project` from the repo root, where
`global.json` selects MTP. `samples/ricaun.NUnit.SampleTests` is the only VSTest
project; its own `global.json` sets `"runner": "VSTest"`, so `cd` there first.
It is for comparison only.

## Pytest bridge

The host pipe (`DevTools_*`) is separate from the MCP pipe (`DevToolsMcp_*`).

Well covered:

- Wire identity: pipe name format and pytest vs MCP discrimination.
- Framing: length-prefixed `BridgeMessage`, distinct from MCP NDJSON.
- Parse and routing: `PytestExecutionService.TryParseRunRequest`, `PytestPathResolver`, unknown method and invalid params on `PytestRequestHandler` / `IpyTestRequestHandler`, `instance/info`.
- Pipe server: `DevToolsPipeServer` start, stop, dispose, and an in-process `instance/info` round trip (`DevToolsPipeServerTests`, unique pipe names).
- Progress vs batch: a null `NotificationSender` creates no progress callback; a set sender does.

Not in headless CI:

- `PytestRunner.py` / `IpyTestDriver.py` in the host, with pixi PEP 723 install.
- Discover, connect, run, report across processes (covered by the client's own tests).

## Execution

Scoped MSTest.Sdk projects ([0034](../decisions/0034-execution-mstest-sdk-scoped-tests.md)):
`CSharp`, `FSharp`, `Python`, `IronPython`, `Pytest`, `Mcp`, `Services`, plus
`Tests.Shared`. `IHostContextExecutor` is an inline mock here; host adapters live
in `RevitDevTool` and `AcadDevTool`, so these tests do not need Revit.exe.

Well covered:

- Python environment: host DLL and version select, uv attach and sidecar stdlib, pixi partition, `IPythonPackageStore` DI, pip-list JSON parse; Parser installed state via list JSON; pixi CLI (`--version`, `--help`) after `SetupPixiAsync`.
- Execution guard: `ExecutionGuardContext` ambient mode and rollback summary.
- MCP dispatch: `IMcpSource.InvokeToolAsync` and resource read; built-in and dotnet backends (`DevTools.Execution.Mcp.Tests`).
- Built-in tools, headless: `open_document` via a mock `IDocumentBridge`; `execute_csharp_code` empty and compile-fail; `execute_python_code` empty (before pythonnet init, else Skip); `run_command` run, add, remove, and list through a real orchestrator and providers with mocked discovery, runner, and script strategies (`RunCommandToolTests`); `add_tools` add, reload, remove, and list through a real `McpCatalogStore` with a fake loader (`AddToolsToolTests`); a rebuilt stream-loaded toolset resolves its new method after the contexts clear (`DotnetMethodResolverTests`); a `CallToolResult` from the toolset's own SDK keeps text and structured content through `DotnetSource` (`DotnetSourceInvokeTests`, needs `samples/McpToolsetDemo` built) and `ResultBridgeTests`.
- Orchestration load: `ExecutionOrchestrator.LoadFromPathAsync` with no provider, covered-root skip, watch and `TreeChanged`.
- C# directives: `#r nuget` and `#load` graph (`CSharpDirectiveParser`).
- File watcher: watch, debounce, unwatch, dispose (`FileWatcherService`).
- Host UI marshal: `RunOnMainThread` inline when the dispatcher is null.
- Assembly isolation: existing isolation fixtures.

Not in headless CI:

- Compile and run on a live Revit or AutoCAD thread (`IHostContextExecutor` in the product hosts).
- Watcher to UI reload: debounce is unit-tested; the execute and reload path is host-only.
- `ToolInvoke.py` on the host: payload mapping has some coverage; live Python.NET invoke is Catalog and host.

`tests/RevitDevTool.PyServer.Tests/` is a small Python parser check.

## Commands

| Goal | Command |
|------|---------|
| MCP tests | `dotnet run --project tests/DevTools.Mcp.Catalog.Tests/DevTools.Mcp.Catalog.Tests.csproj` (also Core, Server, Client, Adapter, `DevTools.Execution.Mcp.Tests`) |
| .NET tests | `dotnet run --project tests/<project>/<project>.csproj` |
| Python parser | `scripts/test-pyserver.ps1` |
| Live MCP | [mcp-integration-test.md](mcp-integration-test.md) |

## Reporting

Name the feature gap (for example "pytest bridge: no in-host `PytestRunner.py`
run"), the missing build artifact, or the missing environment (pixi, host PID).
Claim only what ran: a compile or one project passing is not full platform
verification. Check Current gaps before calling a low coverage number a product
bug.
