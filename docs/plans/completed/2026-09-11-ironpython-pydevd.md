# Execution Plan: IronPython pydevd 2.8.0 listen-on-5680

Date: 2026-09-11

## Status

Completed 2026-09-11 — live PyDev attach, breakpoint, and step proven on
Revit 2025. [0033](../../decisions/0033-ironpython-pydevd-debugger.md) stays
**Proposed** until the product layer is written.

## Outcome

Embedded IronPython uses a **session-lifetime** engine with `Frames`/`FullFrames`,
PyDev.Debugger **2.8.0** on `%APPDATA%\RevitDevTool\pydevd\…`, and a DAP
listener on **5680**. VS Code attaches by itself (no `WaitForClient`).
`*_ipy_script.py` on the embedded path executes on that engine with a real
`co_filename`. When pyRevit is loaded, unattached Run stays pyRevit-first
(0026); an attached PyDev client yields the same file onto the session engine.

## Context

- ADR: [`docs/decisions/0033-ironpython-pydevd-debugger.md`](../../decisions/0033-ironpython-pydevd-debugger.md)
- CPython parallel: `PythonDebugger` + `debugpy.listen` (port **5678**, no wait)
  — `source/DevTools.Execution/Providers/Python/PythonDebugger.cs`
- Zip bootstrap pattern: `UvInstaller` / `PixiInstaller` + `NetworkService`
- AppData root: `AppUtils.GetApplicationDataPath()`
- Embedded IPy today: `IronPythonRunner` create → execute → `Runtime.Shutdown()`
- Revit pyRevit-first: `RevitIPyExecutionStrategy` — **do not change** (0026)
- Host start: `HostBackgroundController.StartAsync` (Revit + AutoCAD)
- Zip:
  `https://github.com/fabioz/PyDev.Debugger/archive/refs/tags/pydev_debugger_2_8_0.zip`
- Skills: `.agents/skills/platform-change/SKILL.md`, `.agents/skills/build/SKILL.md`
- Tests: `tests/DevTools.Execution.Tests/IronPython*.cs` — must keep working
  **without** a live pydevd download
- `docs/agents/test-matrix.md`: do not spawn Coverlet; do not add tests that
  need Revit.exe for this slice

## Scope

In scope:

- `PydevdInstaller` (version-pinned zip + marker, uv-style, fail-open).
- `IronPythonDebugger`: `ConfigureEngine`, `StartListening(5679)`, `IsAttached`
  (`_is_attached`). **No** `WaitForClient` / `_wait_for_attach`.
- Session-lifetime embedded `ScriptEngine` (`Frames` + `FullFrames`); do not
  shutdown after each script.
- Host start: ensure zip + create engine + listen (same gesture as CPython
  init), both Revit and AutoCAD `HostBackgroundController`.
- `IronPythonRunner.Execute` uses that engine; per-run `CreateScope` +
  `CreateScriptSourceFromFile`; refresh script search paths; set/clear
  `__pytest_running__` for `IpyTestDriver.py`.
- Focused tests (installer layout, Frames/`_getframe` when engine created,
  listen no-ops without zip, `IsAttached` false, existing Execute still green).
- `.vscode/launch.json`: `Attach Host: IronPython` → `127.0.0.1:5679` (PyDev
  extension, **not** `"type": "debugpy"`). Confirm the extension’s `type` id
  from its package (likely `pydev`).
- One doc layer: `docs/architecture/Execution/code-execution.md` IronPython
  section (session engine + AppData pydevd + 5679). Link 0033. Do not also
  rewrite `docs/product/execution.md` in this pass.

Out of scope:

- pyRevit `full_frame` / debug through `ScriptExecutor`.
- Merging `IDebuggerBridge` / UI port with CPython.
- `_wait_for_attach`, user-script `import pydevd`, pip/NuGet, forking pydevd,
  `PYDEVD_USE_CYTHON`, pydevd 3.x.
- IPy unittest debug, MCP `execute_*` debug, Coverlet, live VS Code handshake
  as a CI gate (record as validation gap).
- Marking 0033 Accepted.

## Approach

1. **Installer.** `source/DevTools.Execution/Providers/IronPython/PydevdInstaller.cs`
   (or next to debugger). Pin `2.8.0`. Root =
   `Path.Combine(AppUtils.GetApplicationDataPath(), "pydevd",
   "PyDev.Debugger-pydev_debugger_2_8_0")`. Ready iff `pydevd.py` exists and
   `.pydevd-version` is `2.8.0`. Extract the GitHub archive folder as-is; search
   path is that root only. Locked download like uv. Log + continue if network
   fails.

2. **Debugger + session engine.** Public/internal types in
   `Providers/IronPython/`:
   - Create engine with `Frames`/`FullFrames` **once**.
   - `ConfigureEngine(engine)`: insert PyDev root at search-path index 0;
     `import pydevd` is part of listen, not a user script.
   - `StartListening`: set `PydevdCustomization.DEFAULT_PROTOCOL =
     HTTP_JSON_PROTOCOL` **then** `_enable_attach(("127.0.0.1", port))`.
     Preferred 5679, ephemeral fallback, surface `DebugPort`. Catch + log;
     never throw into script Run.
   - `IsAttached` → `pydevd._is_attached()` when imported.
   - Register singleton in `ExecutionExtensions.AddExecutionServices`.

3. **Runner.** Replace per-script create/shutdown with
   `GetOrCreate(bridge)` (lock). First call: stdlib + `bridge.ConfigureEngine` +
   `IronPythonInitializer.Setup` + `StartListening` if installer ready. Later
   calls: new scope, `__file__`, `CreateScriptSourceFromFile`, same compile
   options as today. Do **not** `Runtime.Shutdown()` after Execute. Process
   exit / optional `StopAsync` may shutdown once.

4. **Host start.** Revit and AutoCAD `HostBackgroundController`: after
   `NetworkService.Configure`, `await` pydevd ensure + session init **in
   parallel with** `pythonInitializer.InitializeAsync` if possible. Inject
   `IIronPythonBridge`. Failure of pydevd must not fail add-in start.
   Headless `IronPythonRunner.Execute` in unit tests still lazy-inits the
   same session (no host required); listen skipped if zip missing.

5. **Tests.** `tests/DevTools.Execution.Tests/`:
   - Keep existing Execute / compile / traceback facts green on the shared
     engine (serialize if needed; do not invent a pythonnet-style Skip gate).
   - `CreateEngine` options: `hasattr(sys,"_getframe")` true.
   - Installer: path + marker; Skip download fact if `NetworkService` throws.
   - Debugger: `StartListening` without zip does not throw; `IsAttached`
     false.
   - Optional: if zip already on the machine, smoke `import pydevd` +
     `IS_IRONPYTHON` / `CYTHON_SUPPORTED` — Skip if extract absent.
   - Do **not** assert VS Code attach.

6. **launch.json + architecture doc.** Then compile + focused test.

## Risks And Recovery

- **Zip download / firewall.** Fail-open; scripts still run; no listener.
  Recovery: seed AppData tree by hand, restart host.
- **Shared-engine leakage** (`sys.modules`, `__pytest_running__`). Per-run
  scope + clear the pytest flag. If a test depends on a dead engine, fix the
  test — do not bring back per-script `Shutdown`.
- **Port 5680 taken.** Fallback + log the actual port (settings/UI later).
- **`ConfigureEngine` / stdlib twice.** Only on first create.
- **net48.** Use existing Polyfill; no extra packages.
- **Rollback.** Revert this plan’s files; IPy returns to create/shutdown.
  pyRevit path untouched.
- **PyDev 3.x ↔ 2.8.0 DAP.** Handshake proven live with
  `multi_threads_single_notification = True`.

## Progress

- [x] `PydevdInstaller` + AppData layout + version marker
- [x] `IronPythonDebugger` (protocol, 5680, no wait)
- [x] Session-lifetime engine; `IronPythonRunner` stops shutting down
- [x] Host start hook (Revit + AutoCAD)
- [x] Focused Execution.Tests
- [x] `.vscode/launch.json` IronPython attach
- [x] `docs/architecture/Execution/code-execution.md` IronPython section
- [x] Compile `DevTools.Execution` + focused IronPython/Pydevd tests
- [x] Live PyDev attach + breakpoint + step on Revit 2025
- [x] pyRevit-first Run yields to the session engine while attached

## Decisions

- 2026-09-11: No `WaitForClient` — match CPython listen (0033 decision 5).
- 2026-09-11: Preferred port **5680** (5678 stays CPython `debugpy`).
- 2026-09-11: Session engine required so listen survives until VS Code attaches.
- 2026-09-11: Zip + listen at host start (fail-open); unit tests lazy-init
  without requiring the zip.
- 2026-09-11: One doc layer = architecture `code-execution.md`, not product.
- 2026-09-11: Unattached Revit Run stays pyRevit-first (0026). Attached PyDev
  client yields `*_ipy_script.py` to the session engine; do not `full_frame`
  on ScriptExecutor.
- 2026-09-11: IronPython 3.4 `sys.platform` is not `cli`. Listen sets `cli`
  around pydevd import so 2.8.0’s `IS_IRONPYTHON` / `_current_frames`
  workarounds apply, then forces `IS_WINDOWS=True` and restores. Do not fork
  pydevd.
- 2026-09-11: `_enable_attach` on a dedicated listen thread (not UI,
  not `Task.Run`). API-thread tracing is `enable_tracing()`, not `settrace`
  (`f_back` throws on hosted DLR stacks).
- 2026-09-11: `multi_threads_single_notification = True` so 2.8 HTTP_JSON
  emits DAP `StoppedEvent`.

Promote lasting product or architecture decisions into `docs/decisions/`.
0033 already holds policy; this plan does not fork it.

## Validation

- Focused proof: `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug`
  then `dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj`
  (no `--coverlet`). Filter IronPython / Pydevd facts if the whole project is
  heavy, but do not skip existing Execute facts.
- Integration: live Revit/AutoCAD + PyDev attach to 5680 is **out of CI**
  (proven 2026-09-11 on Revit 2025).
- Repository-required: compile touched Execution (+ host projects if
  `HostBackgroundController` changed, compile-only props). Do not deploy
  unless verifying live.

## Result

Landed. Embedded IPy uses a session Frames/`FullFrames` engine, AppData
pydevd **2.8.0**, listen on **5680** (`HTTP_JSON_PROTOCOL` + `_enable_attach`,
no wait, dedicated listen thread). Host start (Revit + AutoCAD) runs
`InitializeAsync` in parallel with CPython init (fail-open). Unattached
Revit Run stays pyRevit-first (0026). Attached PyDev client yields
`*_ipy_script.py` onto the session engine (`enable_tracing`, real
`co_filename`). Expanding Revit/CLR objects via pydevd `dir`/`getattr` stays
noisy; Watch/console is the supported inspect path.

Proof:

- `dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug -f net8.0-windows` — pass
- `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025` + compile-only props — pass
- `dotnet run --project tests/DevTools.Execution.Tests -- --filter IronPythonDebugger` — **4 passed**
- Live Revit 2025 + PyDev 0.3.0 attach `127.0.0.1:5680`: breakpoint hit, step, iterate

0033 stays Proposed (no `docs/product/execution.md` pass in this plan).
