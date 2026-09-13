# 0025 Runner-Owned Visual Studio Host Attach

Date: 2026-08-27
Amended: 2026-09-13 — Runner attaches the Autodesk host and does not Detach.
Visual Studio owns the debug session after attach (Stop Debugging Detaches
the guest). Testhost stays attached. No AttachLog.

## Status

Accepted. Amends [0016](0016-nunit-native-runtime-and-mtp-first-integration.md)
decision 11: EnvDTE lives in `DevTools.TestRunner.Core/Debugging/`. Does not
reopen 0016 alternative 6 (no generic debugger *wire* protocol).

## Context

Host tests execute inside Autodesk `Revit.exe` / `acad.exe`. Visual Studio
Test Explorer **Debug** attaches to the MTP testhost (`OutputType=Exe`), not
to the Autodesk process. Breakpoints in test bodies miss unless a second
attach targets the host PID **after** the control pipe is up and **before**
`testing/run`:

```text
Test Explorer Debug
  → testhost (VS already attached here)
    → DevTools.TestRunner machine-run (DebugParentPid = testhost)
      → EnsurePipe
      → EnvDTE Process.Attach(hostPid)
      → testhost stays attached (VS launched it)
      → testing/run
```

Visual Studio cannot **Run** tests while the operator is already attached to
the host — Test Explorer **Debug** is the only VS path, and it attaches the
testhost. That is why Runner EnvDTE-attaches VS to the host PID. Testhost is
the process VS launched; Stop Debugging ends that session and Detaches
attached guests (Revit stays alive). EnvDTE lives in TestRunner (always
net10) because testhost may be net48 and the adapter nupkg must not
reference EnvDTE. Attach runs on a short STA with `IOleMessageFilter`.
Testhost stays in `DebuggedProcesses`. Runner does not Detach — EnvDTE
`Detach` while a breakpoint is hit cannot unload the XAML in-app toolbar
(frozen WPF UI). That teardown belongs to Visual Studio’s session end.

Rider and C# Dev Kit **can** attach the Autodesk host and still **Run** tests
(attach does not block the test-execute flow). That is enough to hit
breakpoints in host test bodies. Runner does not attach those IDEs.

Code: `TestCoordinator` + `VisualStudioAttach`. MTP/adapter only sets
`DebugParentPid` when `Debugger.IsAttached` (`HostTestFramework.ApplyDebugParent`).
Architecture tests forbid EnvDTE in the adapter.

`VisualStudioAttach` follows ricaun.RevitTest `VisualStudioDebugUtils`:
`GetActiveObject(VisualStudio.DTE)` (the last-activated devenv), then
`LocalProcesses.OfType<Process>()` for the host PID, then `Attach()`.
After attach, do **not** Detach testhost — that ends VS's session and
Output reports the host "has exited" while the process is still running.
Do **not** Detach the host from Runner. Same as ricaun: Attach before the
run, leave session teardown to Visual Studio. No ROT walk. Enumerate
with `OfType`, not `Item(short)`.

## Decision

### 1. Visual Studio attach stays Runner-owned; the host does not drive IDEs

`DevTools.TestRunner` is the only process that may call EnvDTE to attach to
an Autodesk host PID. The add-in, `DevTools.Execution`, Python initializer,
MCP daemon, and MTP adapter must not `Process.Start` an IDE, mutate
`launch.json`, or call EnvDTE.

- Adapter/MTP: if `Debugger.IsAttached`, pass `DebugParentPid` (testhost
  PID) on the machine-run JSON. That field still implies debug.
- `--debug` and `--debug-parent-pid` stay. Presence of parent PID
  implies debug (adapter sets it when `Debugger.IsAttached`).
- Timing: `EnsurePipeAsync` → attach → `testing/run`. No host
  `run-finishing` event. No Runner Detach on Cancel, Stop Debugging, or
  end of run.
- Attach failure warns on stderr and the run continues.
- After a successful host attach, leave testhost attached. Detach of
  testhost ends VS's debug session and Output reports the host "has exited"
  while the process is still running.
- No `debug-ready` handshake on the host pipe.
- File-backed generations (0016 decision 12) stay the symbol story.

### 2. Hook is VS attach, not an IDE protocol

`IDebuggerAttach` is the Runner-local rename of `IVisualStudioAttach`
(keep `VisualStudioAttach.cs`). It is not a wire protocol and not an
abstraction for Python or for other IDEs.

`GetActiveObject` (`VisualStudio.DTE` 23→9) is the DTE. Attach the host
PID from `LocalProcesses`. `DebugParentPid` only means “this run is Debug”;
it does not select which devenv.

```text
AttachTarget(int HostProcessId, int? ParentProcessId)

IDebuggerAttach
  TryAttach(AttachTarget, warnings) : bool
```

Register `VisualStudioAttach.Instance` in `DevTools.TestRunner`. Do not
register a composite for other IDEs.

| Backend | Mechanism | Confirm | Detach |
|---------|-----------|---------|--------|
| `VisualStudioAttach` | `GetActiveObject` + EnvDTE `Process.Attach` | Operator (IDE session) | Visual Studio (session end / Stop Debugging) |

### 3. Operator attach is the product path outside Visual Studio

Attach the Autodesk host **first**, then **Run** tests (not Test Explorer
**Debug**). Debug-from-testhost still misses in-host breakpoints; the
difference from Visual Studio is that these IDEs allow Run while attached.

| IDE | What to attach | How |
|-----|----------------|-----|
| **Rider** | Host PID (`Revit.exe` / `acad.exe`) | Rider Attach to Process — same PID pick as Visual Studio, then **Run** tests. No repo config file. |
| **C# Dev Kit** (VS Code only) | Host PID | `.vscode/launch.json`: `Attach Host: NetFramework` (`clr`, Revit/AutoCAD 2022–2024 / net48) or `Attach Host: NetCore` (`coreclr`, 2025+). `${command:pickProcess}`. C# Dev Kit is not a VS Code-fork debugger. |
| **VS Code and forks** (Cursor, …) | Python `debugpy` port | `.vscode/launch.json`: `Attach Host: Python` (`debugpy`, `localhost:5678`). Host already listens (`PythonDebugger`, preferred port 5678). CLR/`coreclr` attach does **not** debug Python locals. |
| **PyCharm** (and Rider Python) | Python `debugpy` port | `.run/Attach.run.xml`: `Attach Host: Python` (`PythonDapAttachConfiguration`, `localhost:5678`). |

Do not add Runner `IDebuggerAttach` backends, `launch.json` mutation, or a
JetBrains SDK for these paths.

### 4. Confirmation is Visual Studio EnvDTE; detach is the IDE session

Do not replace VS confirmation with `CheckRemoteDebuggerPresent` (cannot
name which IDE attached). After Debug, Visual Studio Detaches the host when
the testhost session ends.

### 5. What this does not decide

- Python `debugpy.listen` / `IDebuggerBridge` internals (port, not PID).
- Host-side `IDebugController`.
- MCP execute / interactive C# script attach (no testhost, no Runner).
- Changing VS warn-and-continue.
- Unloading the XAML in-app toolbar while the host UI thread is frozen at a
  breakpoint (Visual Studio XAML diagnostics, not EnvDTE).

## Alternatives Considered

1. **Host-side `IDebugController`.** Rejected: wrong owner.
2. **Unify with Python `debugpy.listen`.** Rejected: different runtime / DAP.
3. **Generic host-pipe `debug-ready`.** Rejected (0016 alternative 6).
4. **Fail the test run when attach fails.** Rejected: warn-and-continue.
5. **Require testhost PID in `DebuggedProcesses` before attach.** Rejected:
   Test Explorer’s testhost is often missing there, so attach never ran.
   ricaun’s `GetActiveObject` + `LocalProcesses.Attach(hostPid)` is the
   path that hits breakpoints. Two Visual Studio windows still risk the
   last-activated devenv; that is the same tradeoff ricaun accepted.
6. **Runner EnvDTE `Detach` (Cancel, `run-finishing`, or Stop
   `CommandEvents`).** Rejected: Detach at breakpoint returns success but
   does not unload the XAML toolbar; intercepting Stop Debugging removes
   Revit from `DebuggedProcesses` before Visual Studio can tear down XAML.
   ricaun Attachs and never Detaches from the helper process.

## Consequences

Positive:

- VS Test Explorer Debug attaches the host and leaves testhost attached so
  VS does not end the session mid-run.
- Multiple Visual Studio windows are not auto-detached by the Runner.
- Rider / C# Dev Kit keep operator attach + **Run**; Python keeps listen-on-port.
- MTP, host pipe, and MCP stay unchanged.

Tradeoffs:

- `--debug` CLI without parent PID still attaches via `GetActiveObject`.
- After Debug, the host stays a debuggee until Visual Studio ends the
  testhost session. Stop Debugging at a breakpoint may leave the XAML
  in-app toolbar (frozen WPF UI). Continue then Stop, or disable VS UI
  Debugging Tools for XAML.
- Two Visual Studio windows: `GetActiveObject` is the last-activated devenv.
- Interactive C# / MCP execute debug remains a named gap.
