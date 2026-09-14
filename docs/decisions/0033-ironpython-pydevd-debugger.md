# 0033 IronPython Debug via Vendored PyDev.Debugger 2.8.0

Date: 2026-09-11
Amended: 2026-09-11 — IPy 3.4 `win32`/`cli` shim; port 5680 (later 4567); pyRevit yields
when a debugger client is attached.
Amended: 2026-09-12 — VS Code/Cursor client is `debugpy` attach+connect;
`IpyDebugger.py` handshake + expand-getattr quieting. In-process
debugger remains PyDev.Debugger 2.8.0 on port 4567 (was 5680).
Amended: 2026-09-13 — two IronPython stacks, one debugger.
pyRevit loaded → ScriptExecutor + `IronPythonDebugger.InitializeAsync(engine)`
on the loader engine (`full_frame: false`, reuse). No pyRevit → embedded 3.4.2
via `GetOrCreateEngine`. Do not `CreateEngine` on the pyRevit path.
Amended: 2026-09-14 — live Revit 2024 (.NET 4.8): `import pydevd` succeeds;
`_enable_attach` → `import site` → `os.path.abspath` on CLR `__file__`
(assembly display name). net48 `Path.GetFullPath` rejects it. Wrap
`abspath` in `IpyDebugger.py` before `_enable_attach`. Do not edit AppData
pydevd.

## Status

Accepted.

Companion to [0025](0025-runner-owned-visual-studio-host-attach.md) (CPython
`debugpy` listen-on-port is a **separate** DAP path) and
[0026](0026-ironpython-unittest-script-execution.md) (pyRevit-first when
loaded). Living maps stay in
[`code-execution.md`](../architecture/Execution/code-execution.md) after accept.

## Context

CPython already listens with `PythonDebugger` / `debugpy` on preferred port
5678. That stack is pythonnet / CPython. IronPython (`*_ipy_script.py`) cannot
import `debugpy`, cannot `pip install` a CPython wheel into the DLR engine, and
today creates the embedded engine with `Python.CreateEngine()` **without**
`Frames` unless `IronPythonDebugger` owns the session. pyRevit Labs (when
loaded) uses one `EngineConfigsJson` with `full_frame: false` and
`clean: false` so the pydevd listener survives across Runs.

`sys._getframe` is a hard requirement of PyDev.Debugger. pydevd 2.8.0 source
treats IronPython as a first-class target (`IS_IRONPYTHON = sys.platform ==
'cli'`), includes `_current_frames` / `functools.partial` + `sys.settrace`
workarounds, and disables Cython on non-CPython. That is why 2.8.0 is the
debugger, not “hope Python 2 code runs.”

Embedded IronPython **3.4.2** reports `sys.platform` as `win32` (not `cli`).
pydevd 2.8.0 sets `IS_IRONPYTHON` from `platform == 'cli'` and `IS_WINDOWS`
from `platform == 'win32'` — they cannot both be true from one `sys.platform`.
Listen imports `_pydevd_bundle.pydevd_constants` under `cli` so
`IS_IRONPYTHON` is true, then **forces `IS_WINDOWS = True`**, restores
`sys.platform`, then `import pydevd`. Leaving `IS_WINDOWS` false makes
breakpoint matching case-sensitive (`Samples` vs `samples`, `C:` vs `c:`)
and the hit never fires. Do not fork pydevd.

Live Revit 2024 (.NET 4.8): `import pydevd` is fine. Failure is
`pydevd._enable_attach` → `FilesFiltering` → `import site` →
`os.path.abspath` on CLR `__file__` values that are assembly display names.
net48 `Path.GetFullPath` rejects them (`Specified path is invalid.`); net8+
does not. `IpyDebugger.py` wraps `abspath` before `_enable_attach`. CPython
never throws from `abspath`.

pydevd 2.8.0 DAP attach exists as `_enable_attach` / `_wait_for_attach` /
`_is_attached`, but the **default wire protocol is `QUOTED_LINE_PROTOCOL`**,
not DAP. The 3.x convenience `settrace(..., protocol="dap")` is not in 2.8.0.
`HTTP_JSON_PROTOCOL` is the Content-Length JSON framing DAP uses;
`JSON_PROTOCOL` is raw JSON without headers.

VS Code **Python Debugger (debugpy)** talks DAP directly to the in-process
2.8 listener. pydevd 2.8 does not emit `InitializedEvent` after attach;
`IpyDebugger.py` synthesizes it when `adapterID == "debugpy"`. That
handshake is proven live. Do not import the `debugpy` package into
IronPython (CPython-only). Do not require the PyDev VS Code extension.

## Decision

### 1. IronPython debug is PyDev.Debugger 2.8.0 in-process; VS Code client is debugpy

Adopt **fabioz/PyDev.Debugger tag `pydev_debugger_2_8_0`** as the in-process
debugger for `ExecutionMode.IronPython`. The VS Code/Cursor attach config is
`"type": "debugpy"` connect `localhost:4567` — same client as CPython, not
the same server or socket.

Refuse:

- Importing the `debugpy` package into IronPython (CPython-only; already the
  CPython path).
- pydevd **3.x** as the first IronPython debugger (IronPython is not the
  supported in-process target; protocol APIs differ).
- Unifying this listener with `PythonDebugger` or sharing port 5678
  ([0025](0025-runner-owned-visual-studio-host-attach.md) alternative 2 already
  refused merging DAP stacks).

CPython keeps `debugpy`. CLR/`coreclr` attach still does not debug IronPython
locals.

### 2. Distribution is a version-pinned AppData zip, not a package

On host add-in startup, ensure the zip is present under
`AppUtils.GetApplicationDataPath()` (`%APPDATA%\RevitDevTool`), same owner as
uv/pixi:

- URL:
  `https://github.com/fabioz/PyDev.Debugger/archive/refs/tags/pydev_debugger_2_8_0.zip`
- Extract root (search-path root, contains `pydevd.py`):
  `%APPDATA%\RevitDevTool\pydevd\PyDev.Debugger-pydev_debugger_2_8_0`
- Version marker next to that tree (uv-style). Skip download when the marker
  matches `2.8.0`. Offline hosts keep the last extract.

Refuse: NuGet for pydevd, `pip install pydevd` on IronPython, vendoring the
tree in git, forking/patching pydevd for v1, deleting `.pyd`, or setting
`PYDEVD_USE_CYTHON=0` (2.8.0 already skips Cython on non-CPython).

### 3. Debug engines are created with Frames from the start

```csharp
var engine = IronPython.Hosting.Python.CreateEngine(new Dictionary<string, object>
{
    ["Frames"] = true,
    ["FullFrames"] = true,
});
```

`Frames=true` is required (`sys._getframe`). `FullFrames=true` stays on so the
debugger can inspect locals.

Do **not** set `Tracing=true` (or engine-wide `Debug=true`) at CreateEngine.
IronPython 3.4 then rewrites every method for `sys.settrace` **before**
`import pydevd`; importing 2.8.0 fails with
`Unable to cast … FieldExpression to BlockExpression`. Tracing still starts
when `settrace` is called — user files are compiled **after** that.

`_enable_attach` traces **the calling thread**. Host start is
`HostUiHelper.RunBlocking` on the Revit UI thread, so listen must run on a
**background thread** — same idea as CPython `debugpy.listen`, which does not
pin the debugger to the API thread. Do not `RunOnMainThread` around listen
or `settrace`. If the UI thread is traced at attach time, a breakpoint
suspends Revit and VS Code never shows a hit.

Script **execution** still uses `IHostContextExecutor` because Revit API
requires it, not because pydevd does. Before compiling a user file on that
API thread, call `set_additional_thread_info` + `py_db.enable_tracing()`.
Do not call `pydevd.settrace()` there: 2.8 walks `get_frame().f_back`, and
IronPython 3.4 hosted DLR stacks throw `ArgumentOutOfRangeException`
(`Parameter 'index'`). User code is compiled after this, so existing frames
do not need `f_trace`. `set_trace_to_threads` is CPython-only. Do not skip
`enable_tracing` while already on the debug engine waiting for
`ConfigurationDone`.

Non-debug and debug IronPython share one session engine **only on the
embedded 3.4.2 path** (AutoCAD, Revit without pyRevit). That engine is
created with <c>Frames</c>/<c>FullFrames</c> and **without** <c>Tracing</c>.

When pyRevit is loaded, every `*_ipy_script.py` Run uses pyRevit
<c>ScriptExecutor</c> / <c>PyRevitLoader</c> — not embedded 3.4.2.
<c>IronPythonDebugger.InitializeAsync(object)</c> listens on that loader
engine (no <c>CreateEngine</c>, no <c>Runtime.Shutdown</c>).
<c>EngineConfigsJson</c>: <c>clean:false</c>, <c>persistent:false</c>,
<c>full_frame:false</c>, <c>RefreshEngine:false</c>. Do **not** set
<c>full_frame</c> (pyRevit would set <c>Tracing</c>; IronPython 3.4 then
fails <c>import pydevd</c>). User-extension modules are dropped from
<c>sys.modules</c> before each Run so edited files reload. Embedded 3.4.2
is AutoCAD / Revit-without-pyRevit only. Do not reopen 0026 for routing
(loaded vs not).

### 4. Search path is the PyDev root only

Insert the extract root (the directory that contains `pydevd.py`,
`_pydev_bundle`, `_pydev_imps`, `_pydevd_bundle`) at the front of
`engine.GetSearchPaths()`. Do not add `_pydevd_bundle` or `pydevd_ipython`
as sibling roots.

### 5. Listen like `debugpy`: `_enable_attach` only, never wait

Product attach matches CPython `PythonDebugger`: the host starts a listener
and does **nothing** else. VS Code attaches to the port on its own. Script
Run does not grow a “Debug Script / wait / attach” gesture.

| CPython (`PythonDebugger`) | IronPython (`IronPythonDebugger`) |
|---------------------------|----------------------------------|
| `debugpy.listen(...)` at interpreter init | `pydevd._enable_attach(("127.0.0.1", 4567))` at engine init |
| no `debugpy.wait_for_client()` | no `pydevd._wait_for_attach()` |
| `debugpy.is_client_connected()` | `pydevd._is_attached()` |
| listener lives with the process | same — see decision 8 |

Order when starting the listener (once per host session):

1. `import pydevd` (after the `cli` / `IS_WINDOWS` shim)
2. `PydevdCustomization.DEFAULT_PROTOCOL = HTTP_JSON_PROTOCOL`
3. `pydevd._enable_attach(("127.0.0.1", port))`

Refuse `_wait_for_attach()` / `WaitForClient` as the product path. That API is
`debugpy.wait_for_client()`: it blocks until DAP **`ConfigurationDone`**. This
product does not wait on CPython and will not wait on IronPython.

Refuse relying on 2.8.0’s default `QUOTED_LINE_PROTOCOL`. Refuse
`settrace(..., protocol="dap")` as the 2.8.0 API. `DEFAULT_PROTOCOL` must be
assigned **before** `_enable_attach` because `PyDB()` snapshots it into the
command factory.

After `_enable_attach`, set
`py_db.multi_threads_single_notification = True`. In 2.8.0
`NetCommandFactoryJson.make_thread_suspend_message` is always
`NULL_NET_COMMAND`; the DAP `StoppedEvent` is only emitted from
`make_thread_suspend_single_notification` when that flag is true. The VS
Code PyDev 0.3.0 adapter (bundled pydevd **3.4.1**) still sends a per-thread
`StoppedEvent` when the flag is false, and does **not** send
`multiThreadsSingleNotification` (`# this is now passed in the launch
request`). Without the flag, 2.8 suspends the host thread and waits for
`continue` while VS Code never shows a hit. This is host configuration, not
a pydevd fork.

Preferred port is **4567**. CPython `debugpy` keeps **5678**
([0025](0025-runner-owned-visual-studio-host-attach.md)). The two listeners must
not share a socket. If 4567 is taken, pick an ephemeral port and **surface
it**. VS Code attach is `"type": "debugpy"` connect to `127.0.0.1:4567` (or
the surfaced fallback). Handshake (`InitializedEvent`, unknown DAP command
replies) lives in `IpyDebugger.py`, not a pydevd fork.

Breakpoints at the first lines of a script hit only if the client is already
attached — same as today’s CPython `debugpy` (no wait). Attach after a Run
has finished sees nothing: the script is over.

### 6. Breakpoints require a real `co_filename`

Same contract as CPython (`compile(__source__, __file__, 'exec')`):

1. `Path.GetFullPath(scriptPath)`
2. `File.ReadAllText` (UTF-8)
3. `CreateScriptSourceFromString(code, canonicalPath, SourceCodeKind.File)`
4. `__file__` / `__name__ == '__main__'` on the scope

Refuse `CreateScriptSourceFromFile` as the debug source: DLR file sources can
put a URI or a path that is not the string VS Code sent. Refuse
`engine.Execute(code)` / `<string>`. pydevd matches breakpoints on
filename + line.

### 7. Debugger logic lives in the host, not in user scripts

Shared `DevTools.Execution` owns an `IronPythonDebugger` parallel to
`PythonDebugger` (conceptual API):

```csharp
public sealed class IronPythonDebugger
{
    public void ConfigureEngine(ScriptEngine engine); // search path + import
    public void StartListening(int port = 4567);     // protocol + _enable_attach
    public bool IsAttached { get; }                    // _is_attached
}
```

No `WaitForClient`. Do not inject `import pydevd` / `settrace` into each
`*_ipy_script.py`. Do not put this type in `RevitDevTool` / `AcadDevTool`.
Hosts keep `IIronPythonBridge` for builtins/references only. pyRevit
`SearchPaths` are the command-generator list (script / lib / bin /
pyrevitlib). Do not inject pyRevit paths into the embedded 3.4 engine.

`IDebuggerBridge` surfaces both listen ports (`PythonDebugPort`,
`IronPythonDebugPort`) and one `IsConnected()` (CPython **or** IronPython
client). The two listeners stay on separate sockets.

### 8. Session-lifetime engine: listen at init, execute later, do not shutdown

`debugpy` works without wait because pythonnet is **one process-lifetime
interpreter**; `PythonDebugger.StartListening` runs in `SetupRuntime`, then
any later script is the same `sys.settrace`. Today `IronPythonRunner` does
the opposite: `CreateEngine` → execute → `Runtime.Shutdown()`. A
per-script `_enable_attach` without wait is not attachable: the socket dies
when the engine is shut down, before VS Code can connect.

Therefore the embedded debug engine is **session-lifetime**:

1. Host start (or first embedded IPy init): create Frames engine, add PyDev
   root, `StartListening`, **keep the engine**.
2. User attaches VS Code to 4567 whenever — host has no extra click.
3. `*_ipy_script.py` on the embedded path executes **on that engine**
   (`CreateScriptSourceFromString` with the on-disk path), not on a fresh
   engine that is then shutdown.
4. Do not `Runtime.Shutdown()` that engine at the end of each script.

pyRevit-first Run is [0026](0026-ironpython-unittest-script-execution.md)
plus <c>IronPythonDebugger</c> on the same loader engine. AutoCAD and
Revit-without-pyRevit keep embedded IPy 3.4.2.

`_enable_attach` starts a background listener thread. Do not call it on a
throwaway engine. Do not `_wait_for_attach` on the Revit UI thread.

## Alternatives Considered

1. **debugpy on IronPython.** Rejected. CPython/pythonnet only.
2. **pydevd 3.x as the IronPython debugger.** Rejected for v1. 2.8.0 is the
   last line that documents in-process IronPython (`cli`, `_getframe`,
   Cython off). Revisit only if a later pydevd restores that target.
3. **pip / NuGet / git-vendored tree.** Rejected. IronPython is not a pip
   target here; AppData zip matches uv/pixi bootstrap.
4. **Fork or patch pydevd.** Rejected for v1. Upstream 2.8.0 already has the
   IronPython workarounds.
5. **One `PythonDebugger` / one listen port for both runtimes.** Rejected.
   Different interpreter, different in-process DAP server (debugpy vs
   pydevd 2.8). The **client** type can be the same (`debugpy` attach to
   5678 or 4567). Do not merge sockets.
6. **User-script boilerplate (`import pydevd` in every file).** Rejected.
   Host configures the engine.
7. **Execute debug code as `<string>`.** Rejected. Breakpoints will not bind.
8. **Adopt pyRevit's DLR into a second debugger type.**
   Rejected. One <c>IronPythonDebugger</c>; <c>DlrScriptHost</c> already
   talks to both typed 3.4 and pyRevit's DLR. pyRevit
   `full_frame`/`Tracing` still must not be set (IronPython 3.4 `import pydevd`
   throws <c>FieldExpression</c>/<c>BlockExpression</c>). Do not
   <c>Runtime.Shutdown</c> the loader engine.
9. **`_wait_for_attach` / `WaitForClient` on each debug Run.** Rejected as
   product UX. It is `debugpy.wait_for_client()`, which this product does not
   use. It freezes the host until VS Code attaches and is a different gesture
   from CPython. Keep `_wait_for_attach` out of the API; a spike may still
   call it once to prove DAP handshake, then delete it.
10. **Per-script engine + listen-without-wait.** Rejected. Socket and
    `settrace` die in `ShutdownEngine` before a client can attach. Session-
    lifetime engine is what makes listen-like-debugpy possible.

## Consequences

Positive:

- IronPython debug has an explicit debugger, protocol, layout, and engine
  contract without coupling to CPython `debugpy`.
- No extra NuGet/pip surface; pydevd stays unmodified source on disk.
- Breakpoint identity stays the file the user edits.

Tradeoffs:

- Two listen-on-port stacks and two VS Code attach configurations
  (`debugpy` 5678 CPython, `debugpy` **4567** IronPython). If 4567 is taken,
  the fallback port must be visible.
- pyRevit-loaded Revit runs and debugs on the ScriptExecutor engine
  (`full_frame: false`, reused). AutoCAD / no-pyRevit use embedded 3.4.2.
- Zip download needs network once per machine (or a pre-seeded AppData tree).
- pyRevit `sys.modules` is session-lifetime; user-extension modules are
  dropped before each Run. Embedded IPy is a long-lived Frames engine.
- First-line breakpoints miss unless VS Code is already attached — same as
  CPython `debugpy` without `wait_for_client`.
- Expanding Revit API objects in the Variables view calls `getattr` on
  throwing properties (`FamilyCreate`, `FamilyManager`, worksets). Host
  `IpyDebugger.py` replaces resolver `print_exc` with a one-line
  value and swallows stderr during expand. Do not fork AppData pydevd.
- net48 `Path.GetFullPath` rejects CLR assembly-name `__file__` blobs.
  `IpyDebugger.py` wraps `abspath` before `_enable_attach`.

## Follow-Up

Spike is done when a live IronPython debug Run shows all of:

1. `import pydevd` on the Frames engine
2. `hasattr(sys, "_getframe")` is true
3. `pydevd_constants.IS_IRONPYTHON` is true (`sys.platform == 'cli'`)
4. DAP listener (`HTTP_JSON_PROTOCOL` + `_enable_attach`)
5. VS Code **debugpy** client connects to 4567
6. breakpoint hit on the real `co_filename`
7. locals visible and step-over works

Smoke (no network) before listener:

```python
import sys, platform, pydevd
from _pydevd_bundle import pydevd_constants
# expect: cli, IronPython, _getframe True, pydevd 2.8.0,
# IS_IRONPYTHON True, CYTHON_SUPPORTED False
```

After that spike passes:

- Product layer: `docs/product/execution.md` — IronPython listen-on-4567.
- Architecture: `code-execution.md` — `pydevd` AppData layout, session engine,
  engine flags.
- Optional later: IPy unittest debug through the same loader path.

## References

- Debugger zip:
  https://github.com/fabioz/PyDev.Debugger/archive/refs/tags/pydev_debugger_2_8_0.zip
- Client: VS Code/Cursor `"type": "debugpy"` attach+connect to `localhost:4567`
- Handshake: `source/DevTools.Execution/Resources/scripts/IpyDebugger.py`
- CPython listen: `source/DevTools.Execution/Providers/Python/PythonDebugger.cs`
- Embedded IPy: `source/DevTools.Execution/Providers/IronPython/IronPythonRunner.cs`
- AppData root: `source/DevTools.Utilities/AppUtils.cs`
- pyRevit engine JSON: `source/RevitDevTool/Execution/PyRevit/PyRevitReflectionCache.cs`
